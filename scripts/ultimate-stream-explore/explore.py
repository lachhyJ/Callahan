#!/usr/bin/env python3
"""Offline on/off-field segmentation explorer for un-lapped Ultimate games.

Reads a JSON dump from `garmin_sync.py --dump-stream` (an array of game
objects, each with `metricDescriptors` + `activityDetailMetrics`) and tries to
recover which stretches of a game were spent on the field vs the sideline,
from the GPS-speed and heart-rate streams alone - no lap presses.

This is NOT wired into Callahan. Pure analysis. The output is an eyeball
report: per-game on/off minutes, a count of on-field stretches (~ points
played), and a one-char-per-minute ASCII timeline to lay against memory of
the game. The question it answers is "does on/off-field separate cleanly
enough to be worth building on?".

    python3 explore.py tourney-stream.json [more.json ...]
    python3 explore.py --self-test        # synthetic-fixture sanity check

stdlib only (Python 3.11+).
"""

import argparse
import json
import math
import statistics
import sys

# --- tuning knobs (all in one place; this is exploration, expect to fiddle) ---
SMOOTH_WINDOWS_SEC = (30, 60, 90)   # reported; 60 is the one segmentation uses
SMOOTH_PRIMARY_SEC = 60
HYSTERESIS_LOW_FRAC = 0.6           # exit threshold = enter threshold * this
MIN_DWELL_SEC = 45                  # segments shorter than this get merged away
HR_BASELINE_WINDOW_SEC = 300
HR_MARGIN_BPM = 6                   # HR must clear baseline by this to read "on"
HR_CONFIRM_WINDOW_SEC = 90         # speed+hr: HR must move the right way within this

# Garmin get_activity_details descriptor keys vary; try these in order.
KEY_CANDIDATES = {
    "time": ("directTimestamp", "sumElapsedDuration", "sumDuration", "sumMovingDuration"),
    "speed": ("directSpeed", "directSpeedWithGaps", "sumMovingSpeed"),
    "hr": ("directHeartRate", "directHeartRateWithGaps"),
    "lat": ("directLatitude",),
    "lon": ("directLongitude",),
}


# --------------------------------------------------------------------------- #
# parsing
# --------------------------------------------------------------------------- #
class Game:
    def __init__(self, name, start, dur_s, t, spd, hr):
        self.name = name
        self.start = start
        self.dur_s = dur_s
        self.t = t            # seconds from activity start, monotonic
        self.spd = spd        # m/s per sample (list, no None once parsed)
        self.hr = hr          # bpm per sample, or None if the whole stream lacks HR
        self.speed_derived = False


def _resolve_indices(descriptors):
    key_to_idx = {d.get("key"): d.get("metricsIndex") for d in descriptors or []
                  if d.get("key") is not None and d.get("metricsIndex") is not None}
    out = {}
    for field, candidates in KEY_CANDIDATES.items():
        out[field] = next((key_to_idx[c] for c in candidates if c in key_to_idx), None)
    return out, key_to_idx


def _haversine_m(lat1, lon1, lat2, lon2):
    r = 6371000.0
    p1, p2 = math.radians(lat1), math.radians(lat2)
    dp = math.radians(lat2 - lat1)
    dl = math.radians(lon2 - lon1)
    a = math.sin(dp / 2) ** 2 + math.cos(p1) * math.cos(p2) * math.sin(dl / 2) ** 2
    return 2 * r * math.asin(min(1.0, math.sqrt(a)))


def parse_game(obj):
    descriptors = obj.get("metricDescriptors")
    rows = obj.get("activityDetailMetrics") or []
    idx, key_to_idx = _resolve_indices(descriptors)
    if idx["time"] is None or len(rows) < 10:
        raise ValueError(f"{obj.get('activityName')!r}: no usable time metric or too few samples "
                         f"({len(rows)})")

    time_is_ms = key_to_idx.get("directTimestamp") == idx["time"]

    def col(i):
        if i is None:
            return None
        vals = []
        for r in rows:
            m = r.get("metrics")
            vals.append(m[i] if m and i < len(m) else None)
        return vals

    raw_t = col(idx["time"])
    t0 = next(v for v in raw_t if v is not None)
    t = [((v - t0) / 1000.0 if time_is_ms else (v - t0)) if v is not None else None for v in raw_t]

    spd = col(idx["speed"])
    hr = col(idx["hr"])
    lat, lon = col(idx["lat"]), col(idx["lon"])

    # keep only samples with a timestamp; forward-fill small gaps in speed/hr
    keep = [k for k, tv in enumerate(t) if tv is not None]
    t = [t[k] for k in keep]
    spd = [spd[k] for k in keep] if spd else None
    hr = [hr[k] for k in keep] if hr else None
    lat = [lat[k] for k in keep] if lat else None
    lon = [lon[k] for k in keep] if lon else None

    speed_derived = False
    if not spd or sum(1 for v in spd if v is not None) < len(t) * 0.5:
        if not (lat and lon):
            raise ValueError(f"{obj.get('activityName')!r}: no speed metric and no GPS to derive it from")
        spd = [0.0]
        for k in range(1, len(t)):
            dt = t[k] - t[k - 1]
            if dt <= 0 or lat[k] is None or lat[k - 1] is None:
                spd.append(spd[-1])
                continue
            spd.append(_haversine_m(lat[k - 1], lon[k - 1], lat[k], lon[k]) / dt)
        speed_derived = True

    spd = _fill(spd, 0.0)
    hr = _fill(hr, None) if hr and any(v is not None for v in hr) else None

    g = Game(obj.get("activityName"), obj.get("startTimeLocal"),
             obj.get("durationSeconds"), t, spd, hr)
    g.speed_derived = speed_derived
    return g


def _fill(seq, default):
    """Forward-fill None; leading Nones become the first real value or default."""
    out = list(seq)
    last = None
    for i, v in enumerate(out):
        if v is None:
            out[i] = last
        else:
            last = v
    if out[0] is None:
        first = next((v for v in out if v is not None), default)
        out = [first if v is None else v for v in out]
    return out


# --------------------------------------------------------------------------- #
# segmentation primitives
# --------------------------------------------------------------------------- #
def rolling_median_time(t, y, window_s):
    """Median of y over a centred +/- window/2 time window, per sample."""
    n = len(t)
    out = [0.0] * n
    lo = hi = 0
    half = window_s / 2
    for i in range(n):
        while lo < n and t[lo] < t[i] - half:
            lo += 1
        if hi < lo:
            hi = lo
        while hi < n and t[hi] <= t[i] + half:
            hi += 1
        out[i] = statistics.median(y[lo:hi]) if hi > lo else y[i]
    return out


def otsu_threshold(values, bins=64):
    """1-D Otsu on log1p(values) - returns the split back in linear units."""
    xs = [math.log1p(max(0.0, v)) for v in values]
    lo, hi = min(xs), max(xs)
    if hi - lo < 1e-9:
        return math.expm1(lo)
    width = (hi - lo) / bins
    hist = [0] * bins
    for x in xs:
        b = min(bins - 1, int((x - lo) / width))
        hist[b] += 1
    total = len(xs)
    sum_all = sum((lo + (b + 0.5) * width) * hist[b] for b in range(bins))
    w_bg = 0.0
    sum_bg = 0.0
    best_var, best_x = -1.0, lo + width
    for b in range(bins):
        w_bg += hist[b]
        if w_bg == 0:
            continue
        w_fg = total - w_bg
        if w_fg == 0:
            break
        sum_bg += (lo + (b + 0.5) * width) * hist[b]
        m_bg = sum_bg / w_bg
        m_fg = (sum_all - sum_bg) / w_fg
        var_between = w_bg * w_fg * (m_bg - m_fg) ** 2
        if var_between > best_var:
            best_var, best_x = var_between, lo + (b + 1) * width
    return math.expm1(best_x)


def hysteresis_label(t, y, enter, exitv):
    """Per-sample bool: True above `enter`, False below `exitv`, carry between."""
    state = y[0] >= enter
    out = []
    for v in y:
        if v >= enter:
            state = True
        elif v < exitv:
            state = False
        out.append(state)
    return out


def merge_short_segments(t, labels, min_dwell_s):
    """Flip any run whose duration < min_dwell to match the run before it."""
    if not labels:
        return labels
    out = list(labels)
    i = 0
    n = len(out)
    while i < n:
        j = i
        while j < n and out[j] == out[i]:
            j += 1
        seg_dur = (t[j - 1] - t[i]) if j > i else 0
        if seg_dur < min_dwell_s and i > 0:
            fill = out[i - 1]
            for k in range(i, j):
                out[k] = fill
            # restart from the merged run's start so cascading merges settle
            i = i - 1 if i > 0 else 0
            while i > 0 and out[i - 1] == out[i]:
                i -= 1
        else:
            i = j
    return out


def segments_from_labels(t, labels):
    """List of (state_bool, start_s, end_s). end is the last sample's t."""
    segs = []
    i = 0
    n = len(labels)
    while i < n:
        j = i
        while j < n and labels[j] == labels[i]:
            j += 1
        segs.append((labels[i], t[i], t[j - 1]))
        i = j
    return segs


def label_stats(t, labels):
    on_s = off_s = 0.0
    for i in range(1, len(t)):
        dt = t[i] - t[i - 1]
        if labels[i - 1]:
            on_s += dt
        else:
            off_s += dt
    stretches = sum(1 for k, (st, _, _) in enumerate(segments_from_labels(t, labels)) if st)
    return on_s, off_s, stretches


# --------------------------------------------------------------------------- #
# the three segmenters
# --------------------------------------------------------------------------- #
def segment_speed(g):
    sm = rolling_median_time(g.t, g.spd, SMOOTH_PRIMARY_SEC)
    enter = otsu_threshold(sm)
    exitv = enter * HYSTERESIS_LOW_FRAC
    labels = merge_short_segments(g.t, hysteresis_label(g.t, sm, enter, exitv), MIN_DWELL_SEC)
    return labels, {"enter": enter, "exit": exitv, "smoothed": sm}


def segment_hr(g):
    if not g.hr:
        return None, {}
    base = rolling_median_time(g.t, g.hr, HR_BASELINE_WINDOW_SEC)
    rel = [h - b for h, b in zip(g.hr, base)]
    labels = merge_short_segments(
        g.t, hysteresis_label(g.t, rel, HR_MARGIN_BPM, -HR_MARGIN_BPM), MIN_DWELL_SEC)
    return labels, {"margin": HR_MARGIN_BPM}


def segment_speed_hr(g, speed_labels):
    """Speed leads; downgrade (mark unconfirmed) any boundary HR doesn't back
    within HR_CONFIRM_WINDOW_SEC. Boundaries are kept either way - this reports
    confidence, it doesn't overrule speed."""
    if not g.hr:
        return speed_labels, {"unconfirmed": None}
    base = rolling_median_time(g.t, g.hr, HR_BASELINE_WINDOW_SEC)
    rel = [h - b for h, b in zip(g.hr, base)]
    unconfirmed = 0
    for i in range(1, len(speed_labels)):
        if speed_labels[i] == speed_labels[i - 1]:
            continue
        going_on = speed_labels[i]
        # HR change expected in the same direction, within the window after
        lo_t = g.t[i]
        window_rel = [rel[k] for k in range(i, len(g.t)) if g.t[k] - lo_t <= HR_CONFIRM_WINDOW_SEC]
        if not window_rel:
            unconfirmed += 1
            continue
        moved = max(window_rel) if going_on else -min(window_rel)
        if moved < HR_MARGIN_BPM:
            unconfirmed += 1
    return speed_labels, {"unconfirmed": unconfirmed}


# --------------------------------------------------------------------------- #
# reporting
# --------------------------------------------------------------------------- #
def ascii_timeline(t, labels, minutes=None):
    total_min = int(math.ceil(t[-1] / 60)) if minutes is None else minutes
    chars = []
    for m in range(total_min):
        lo, hi = m * 60, (m + 1) * 60
        on = off = 0.0
        for i in range(1, len(t)):
            if t[i - 1] >= hi or t[i] <= lo:
                continue
            seg = min(t[i], hi) - max(t[i - 1], lo)
            if labels[i - 1]:
                on += seg
            else:
                off += seg
        if on == 0 and off == 0:
            chars.append(" ")
        else:
            chars.append("#" if on >= off else ".")
    return "".join(chars)


def report_game(idx, g):
    print(f"\nGame {idx}  {g.name!r}  {g.start or '?'}")
    dur_min = g.t[-1] / 60
    src = " (speed derived from GPS)" if g.speed_derived else ""
    hrn = "" if g.hr else "  [no HR stream]"
    print(f"  {len(g.t)} samples over {dur_min:.1f} min{src}{hrn}")

    for w in SMOOTH_WINDOWS_SEC:
        sm = rolling_median_time(g.t, g.spd, w)
        thr = otsu_threshold(sm)
        print(f"  smooth {w:>2}s -> otsu enter threshold {thr:.2f} m/s")

    sp_labels, sp_meta = segment_speed(g)
    on_s, off_s, n_on = label_stats(g.t, sp_labels)
    tot = on_s + off_s or 1
    print(f"  speed-only  (enter {sp_meta['enter']:.2f} / exit {sp_meta['exit']:.2f} m/s): "
          f"on {on_s/60:.1f} min ({on_s/tot:.0%})  off {off_s/60:.1f} min  {n_on} on-field stretches")

    hr_labels, _ = segment_hr(g)
    if hr_labels:
        h_on, h_off, h_n = label_stats(g.t, hr_labels)
        htot = h_on + h_off or 1
        print(f"  hr-only:    on {h_on/60:.1f} min ({h_on/htot:.0%})  off {h_off/60:.1f} min  "
              f"{h_n} stretches  (HR lags ~60-120s, expect smeared boundaries)")

    combo_labels, combo_meta = segment_speed_hr(g, sp_labels)
    unconf = combo_meta.get("unconfirmed")
    tag = "" if unconf is None else f"  ({unconf} boundaries HR-unconfirmed)"
    print(f"  speed+hr:   same on/off as speed-only; HR as a confidence check{tag}")
    print(f"  timeline (1 char/min, '#' on-field '.' sideline):")
    tl = ascii_timeline(g.t, combo_labels)
    for k in range(0, len(tl), 60):
        print(f"    {tl[k:k+60]}")

    return {"enter": sp_meta["enter"], "on_frac": on_s / tot, "stretches": n_on}


def report_tournament(rows):
    if len(rows) < 2:
        return
    print("\n" + "=" * 64)
    print(f"Tournament summary ({len(rows)} games)")
    enters = [r["enter"] for r in rows]
    fracs = [r["on_frac"] for r in rows]
    print("  enter threshold m/s : " + "  ".join(f"{e:.2f}" for e in enters)
          + f"   (sd {statistics.pstdev(enters):.2f})")
    print("  on-field fraction   : " + "  ".join(f"{f:.2f}" for f in fracs))
    # crude linear trend across game order
    n = len(fracs)
    xs = list(range(n))
    mx, my = statistics.mean(xs), statistics.mean(fracs)
    denom = sum((x - mx) ** 2 for x in xs) or 1
    slope = sum((x - mx) * (f - my) for x, f in zip(xs, fracs)) / denom
    trend = "fatigue?" if slope < -0.01 else ("warming up?" if slope > 0.01 else "flat")
    print(f"  on-field trend      : {slope:+.3f} / game  ({trend})")
    print("  stretch count       : " + "  ".join(str(r["stretches"]) for r in rows))


# --------------------------------------------------------------------------- #
# self-test
# --------------------------------------------------------------------------- #
def _synth_game(seed=1):
    import random
    rng = random.Random(seed)
    t, spd, hr = [], [], []
    now = 0.0
    on = True
    hr_now = 150.0
    # 10 on-field points (180s) alternating with 10 sidelines (120s) = 50 min
    for _ in range(10):
        for seg_dur, base_spd, target_hr in (
            (180, 3.0, 172),   # on-field: bursts around 3 m/s
            (120, 0.4, 138),   # sideline: near-still
        ):
            end = now + seg_dur
            while now < end:
                now += rng.uniform(2.0, 3.0)
                if base_spd > 1:
                    s = max(0.0, rng.gauss(base_spd, 1.6))   # wide swings within a point
                else:
                    s = max(0.0, rng.gauss(base_spd, 0.3))
                hr_now += (target_hr - hr_now) * 0.04 + rng.gauss(0, 1.5)
                t.append(now)
                spd.append(s)
                hr.append(hr_now)
    descriptors = [
        {"key": "sumElapsedDuration", "metricsIndex": 0},
        {"key": "directSpeed", "metricsIndex": 1},
        {"key": "directHeartRate", "metricsIndex": 2},
    ]
    rows = [{"metrics": [tt, ss, hh]} for tt, ss, hh in zip(t, spd, hr)]
    return {"activityName": "SYNTH", "startTimeLocal": None, "durationSeconds": now,
            "metricDescriptors": descriptors, "activityDetailMetrics": rows}


def self_test():
    g = parse_game(_synth_game())
    labels, meta = segment_speed(g)
    on_s, off_s, n_on = label_stats(g.t, labels)
    frac = on_s / (on_s + off_s)
    print(f"self-test: enter={meta['enter']:.2f} m/s  on-fraction={frac:.2f} "
          f"(true 0.60)  stretches={n_on} (true 10)")
    ok = (0.48 <= frac <= 0.72) and (7 <= n_on <= 13)
    print("PASS" if ok else "FAIL")
    return 0 if ok else 1


# --------------------------------------------------------------------------- #
def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("files", nargs="*", help="JSON dump(s) from garmin_sync.py --dump-stream")
    ap.add_argument("--self-test", action="store_true", help="Run the synthetic-fixture check and exit.")
    args = ap.parse_args()

    if args.self_test:
        sys.exit(self_test())
    if not args.files:
        ap.error("give at least one --dump-stream JSON file, or --self-test")

    games = []
    for path in args.files:
        with open(path) as f:
            payload = json.load(f)
        for obj in (payload if isinstance(payload, list) else [payload]):
            try:
                games.append(parse_game(obj))
            except ValueError as e:
                print(f"  skipped: {e}", file=sys.stderr)

    if not games:
        print("No parseable games in the input.", file=sys.stderr)
        sys.exit(1)

    rows = [report_game(i + 1, g) for i, g in enumerate(games)]
    report_tournament(rows)


if __name__ == "__main__":
    main()
