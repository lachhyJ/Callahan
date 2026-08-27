#!/usr/bin/env python3
"""Diagnostic instrumentation for the on/off-field geometry classifier.

Loads the six committed test fixtures directly (gzipped {t,lat,lon,spd}
arrays — the same shape PUT /api/activities/{id}/track stores) and re-runs
segment.py's analyse() with extra instrumentation:

  - fitted theta/halfw/halfl per game, and the derived centre threshold
    (halfw * CENTRE_FACTOR)
  - which disjunct (spread-only / centre-only / both) accounts for each
    on-field second, BEFORE the MIN_DWELL merge step
  - a before/after picture of trimming the track to [first point start,
    last point start + FOLLOW_S] — i.e. dropping pre-game warmup and any
    trailing cooldown/handshake time (does NOT touch a mid-game halftime
    gap, which is reported separately as the largest inter-point gap)
  - a small parameter sweep over CENTRE_FACTOR / MIN_DWELL / WIN

Run: python3 diagnose.py
"""
import gzip
import json
import math
import os
import statistics

HERE = os.path.dirname(os.path.abspath(__file__))
FIXTURES = os.path.normpath(os.path.join(HERE, "..", "..", "tests", "Callahan.Api.Tests", "Fixtures"))

# Mirrors FieldGeometryOptions.Default in backend/Services/FieldGeometry.cs
WIN = 100.0
FAST = 4.0
MIN_DWELL = 75.0
SPREAD_FACTOR = 0.8
CENTRE_FACTOR = 0.55
EZ_FRAC = 0.55
EZ_MIN_S = 25.0
EZ_MAX_SPD = 2.5
FOLLOW_S = 90.0
FOLLOW_FRAC = 0.6


def project(t, lat, lon, spd, fast=FAST):
    mla = statistics.mean(lat)
    mlo = statistics.mean(lon)
    mlat, mlon = 111320.0, 111320.0 * math.cos(math.radians(mla))
    xy = [((lon[i] - mlo) * mlon, (lat[i] - mla) * mlat, spd[i]) for i in range(len(t))]
    fastpts = [(x, y) for x, y, s in xy if s >= fast]
    src = fastpts if len(fastpts) >= 40 else [(x, y) for x, y, _ in xy]
    mx = statistics.mean(p[0] for p in src)
    my = statistics.mean(p[1] for p in src)
    cxx = statistics.pvariance([p[0] for p in src])
    cyy = statistics.pvariance([p[1] for p in src])
    cxy = sum((p[0] - mx) * (p[1] - my) for p in src) / len(src)
    th = 0.5 * math.atan2(2 * cxy, cxx - cyy)
    ct, st = math.cos(th), math.sin(th)
    along, cross = [], []
    for x, y, s in xy:
        dx, dy = x - mx, y - my
        along.append(dx * ct + dy * st)
        cross.append(-dx * st + dy * ct)
    fc = [c for c, s in zip(cross, spd) if s >= fast]
    c0 = statistics.median(fc) if len(fc) >= 40 else statistics.median(cross)
    return along, [c - c0 for c in cross], th


def roll(t, vals, win, fn):
    n = len(t)
    out = [0.0] * n
    lo = hi = 0
    for i in range(n):
        while t[lo] < t[i] - win / 2:
            lo += 1
        if hi < lo:
            hi = lo
        while hi < n and t[hi] <= t[i] + win / 2:
            hi += 1
        out[i] = fn(vals[lo:hi]) if hi > lo else fn([vals[i]])
    return out


def spread(xs):
    if len(xs) < 4:
        return 0.0
    s = sorted(xs)
    n = len(s)
    return s[int(n * 0.9)] - s[int(n * 0.1)]


def merge(t, lab, mind):
    out = list(lab)
    i = 0
    while i < len(out):
        j = i
        while j < len(out) and out[j] == out[i]:
            j += 1
        if j > i and (t[j - 1] - t[i]) < mind and i > 0:
            for k in range(i, j):
                out[k] = out[i - 1]
            i = 0
        else:
            i = j
    return out


def runs(lab):
    r = []
    i = 0
    while i < len(lab):
        j = i
        while j < len(lab) and lab[j] == lab[i]:
            j += 1
        r.append((lab[i], i, j - 1))
        i = j
    return r


def points_played(t, along, spd, onfield, halfl,
                   ez_frac=EZ_FRAC, ez_min_s=EZ_MIN_S, ez_max_spd=EZ_MAX_SPD,
                   follow_s=FOLLOW_S, follow_frac=FOLLOW_FRAC):
    inez = [abs(a) > halfl * ez_frac for a in along]
    pts = 0
    starts = []
    for state, i0, i1 in runs(inez):
        if not state or t[i1] - t[i0] < ez_min_s:
            continue
        if statistics.mean(spd[i0:i1 + 1]) > ez_max_spd:
            continue
        if sum(onfield[i0:i1 + 1]) < (i1 - i0 + 1) / 2:
            continue
        j = i1
        while j < len(t) - 1 and t[j] - t[i1] < follow_s:
            j += 1
        follow = onfield[i1:j + 1]
        if not follow or sum(follow) < len(follow) * follow_frac:
            continue
        pts += 1
        starts.append(t[i1])
    return pts, starts


def analyse(t, lat, lon, spd, win=WIN, spread_factor=SPREAD_FACTOR,
            centre_factor=CENTRE_FACTOR, min_dwell=MIN_DWELL):
    along, cross, th = project(t, lat, lon, spd)
    fc = sorted(abs(c) for c, s in zip(cross, spd) if s >= FAST)
    halfw = fc[int(len(fc) * 0.9)] if len(fc) > 20 else 18.0
    fa = sorted(abs(a) for a, s in zip(along, spd) if s >= FAST)
    halfl = fa[int(len(fa) * 0.9)] if len(fa) > 20 else 45.0

    lat_spread = roll(t, cross, win, spread)
    abs_cross = roll(t, [abs(c) for c in cross], win, statistics.median)

    spread_hit = [ls > halfw * spread_factor for ls in lat_spread]
    centre_hit = [ac < halfw * centre_factor for ac in abs_cross]
    onraw = [sp or ce for sp, ce in zip(spread_hit, centre_hit)]
    onfield = merge(t, onraw, min_dwell)

    pts, starts = points_played(t, along, spd, onfield, halfl)

    on = sum(t[i] - t[i - 1] for i in range(1, len(t)) if onfield[i - 1])
    dur = t[-1] - t[0]

    return {
        "halfw": halfw, "halfl": halfl, "theta_deg": math.degrees(th),
        "centre_thresh": halfw * centre_factor,
        "spread_hit": spread_hit, "centre_hit": centre_hit,
        "onraw": onraw, "onfield": onfield,
        "on": on, "dur": dur, "pts": pts, "starts": starts,
        "t": t, "along": along, "cross": cross, "spd": spd,
    }


def disjunct_breakdown(t, spread_hit, centre_hit, onraw):
    """Seconds of RAW (pre-merge) on-field time won by each disjunct."""
    spread_only = centre_only = both = 0.0
    for i in range(1, len(t)):
        if not onraw[i - 1]:
            continue
        dt = t[i] - t[i - 1]
        sp, ce = spread_hit[i - 1], centre_hit[i - 1]
        if sp and ce:
            both += dt
        elif sp:
            spread_only += dt
        elif ce:
            centre_only += dt
    return spread_only, centre_only, both


def load_fixture(path):
    with gzip.open(path, "rt") as f:
        d = json.load(f)
    s = d["samples"]
    return [float(x) for x in s["t"]], s["lat"], s["lon"], s["spd"]


def main():
    games = sorted(f for f in os.listdir(FIXTURES) if f.startswith("game-") and f.endswith(".json.gz"))
    baselines = json.load(open(os.path.join(FIXTURES, "baselines.json")))["games"]

    print("=" * 100)
    print("STEP 1: fitted field geometry + which disjunct wins on-field time (pre-merge)")
    print("=" * 100)
    results = []
    for gi, fname in enumerate(games, 1):
        t, lat, lon, spd = load_fixture(os.path.join(FIXTURES, fname))
        r = analyse(t, lat, lon, spd)
        results.append((fname, r))
        sp_only, ce_only, both = disjunct_breakdown(t, r["spread_hit"], r["centre_hit"], r["onraw"])
        raw_on = sp_only + ce_only + both
        name = baselines[gi - 1]["name"]
        print(f"\n{fname}  ({name})")
        print(f"  field fit: {2*r['halfw']:.1f} m wide x {2*r['halfl']:.1f} m long"
              f"  (real field ~37m x 64m)  | theta={r['theta_deg']:.1f} deg")
        print(f"  centre threshold = halfw * {CENTRE_FACTOR} = {r['centre_thresh']:.1f} m")
        print(f"  raw on-field (pre-merge): {raw_on/60:.1f} min"
              f"  [spread-only {sp_only/60:.1f}m ({sp_only/raw_on:.0%})"
              f"  centre-only {ce_only/60:.1f}m ({ce_only/raw_on:.0%})"
              f"  both {both/60:.1f}m ({both/raw_on:.0%})]")
        print(f"  final on-field (post-merge): {r['on']/60:.1f}/{r['dur']/60:.1f} min"
              f" ({r['on']/r['dur']:.0%})  pts={r['pts']}")

    print()
    print("=" * 100)
    print("STEP 2: point-start gaps (largest gap = likely halftime), and warmup/tail time")
    print("=" * 100)
    for fname, r in results:
        starts = r["starts"]
        t = r["t"]
        pre = starts[0] - t[0] if starts else None
        post = t[-1] - starts[-1] if starts else None
        gaps = [starts[i + 1] - starts[i] for i in range(len(starts) - 1)]
        biggest = max(gaps) if gaps else None
        print(f"{fname}: pre-first-point {pre and pre/60:.1f} min, "
              f"post-last-point-start {post and post/60:.1f} min, "
              f"largest inter-point gap {biggest and biggest/60:.1f} min"
              + (f" (median gap {statistics.median(gaps)/60:.1f} min)" if gaps else ""))

    print()
    print("=" * 100)
    print("STEP 3: trim to [first point start - 60s, last point start + FOLLOW_S], re-score")
    print("=" * 100)
    print(f"{'game':30} {'orig on%':>9} {'trim on%':>9} {'orig pts':>9} {'trim pts':>9}")
    for fname, r in results:
        t_arr, lat, lon, spd = load_fixture(os.path.join(FIXTURES, fname))
        starts = r["starts"]
        if not starts:
            print(f"{fname:30}  (no points detected, skipping trim)")
            continue
        lo = starts[0] - 60
        hi = starts[-1] + FOLLOW_S
        keep = [i for i, tt in enumerate(t_arr) if lo <= tt <= hi]
        t2 = [t_arr[i] for i in keep]
        lat2 = [lat[i] for i in keep]
        lon2 = [lon[i] for i in keep]
        spd2 = [spd[i] for i in keep]
        r2 = analyse(t2, lat2, lon2, spd2)
        print(f"{fname:30} {r['on']/r['dur']:>8.0%} {r2['on']/r2['dur']:>8.0%} {r['pts']:>9} {r2['pts']:>9}")

    print()
    print("=" * 100)
    print("STEP 4: parameter sweep (all six games, aggregate on%, min pts, max pts)")
    print("=" * 100)
    all_fixtures = [load_fixture(os.path.join(FIXTURES, f)) for f in games]

    def sweep(label, **kwargs):
        tot_on = tot_dur = 0.0
        pts_list = []
        for t, lat, lon, spd in all_fixtures:
            r = analyse(t, lat, lon, spd, **kwargs)
            tot_on += r["on"]
            tot_dur += r["dur"]
            pts_list.append(r["pts"])
        print(f"{label:45} agg on%={tot_on/tot_dur:>5.0%}  pts={pts_list}  total={sum(pts_list)}")

    sweep("baseline (defaults)")
    for cf in (0.35, 0.40, 0.45, 0.50, CENTRE_FACTOR):
        sweep(f"CENTRE_FACTOR={cf}", centre_factor=cf)
    for md in (30, 45, 60, MIN_DWELL, 90, 120):
        sweep(f"MIN_DWELL={md}", min_dwell=md)
    for w in (40, 60, 80, WIN, 120):
        sweep(f"WIN={w}", win=w)
    # combined: tighter centre + shorter min-dwell (undoes the asymmetric merge)
    for cf in (0.40, 0.45):
        for md in (45, 60):
            sweep(f"CENTRE_FACTOR={cf} + MIN_DWELL={md}", centre_factor=cf, min_dwell=md)


if __name__ == "__main__":
    main()
