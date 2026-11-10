#!/usr/bin/env python3
"""Reference implementation of FieldGeometry v5 (backend/Services/FieldGeometry.cs).

The field detector that replaced segment.py's spread/centre classifier in
2026-11. Validated against four lap-pressed club tryouts (leave-one-out: tune
on three, score the fourth) and judged on field plots against Lachlan's own
reads. Steps:

1. Fit set: fast samples (>= FAST m/s) in the area with the most fast running
   (main_cluster). Lap-pressed sessions also drop warmup / rest / ambiguous
   laps from the fit (fit_exclusion).
2. Gameplay start: the first line-up of a run of line-ups (end dwell followed
   by a burst) that alternates ends like games, not drills. 0 when none found.
   Never keyed on lap position.
3. Geometry from fast play after the start: angle = PCA; endzone lines = the
   median of line-up dwell |along| per end, re-centred symmetric; width = the
   3rd-97th percentile span of fast cross, centred between them.
4. On-field = inside the box on a short rolling median, minus sustained slow
   stretches (no sample above SlowMaxMps for >= SlowMinSec), and never before
   the gameplay start. (The prototype also exempted line-ups up to 150 s,
   inert once SlowMinSec rose to 150; dropped.)
5. Points played = on-field stints (gaps < STINT_GAP bridged, >= STINT_MIN
   long), each split at internal line-up + burst >= SPLIT_MIN after the last
   split. Rest periods = off gaps >= REST_MIN between stints.

The C# mirrors this function for function; P mirrors FieldGeometryOptions and
is checked against the C# record on import.

    python3 fieldgeometry_v2.py            # summary over the committed fixtures
"""
import gzip
import json
import math
import os
import re
import statistics as st
import sys
from collections import Counter

HERE = os.path.dirname(os.path.abspath(__file__))
FIXTURES = os.path.normpath(os.path.join(HERE, "..", "..", "tests", "Callahan.Api.Tests", "Fixtures"))
FIELD_GEOMETRY_CS = os.path.normpath(os.path.join(HERE, "..", "..", "backend", "Services", "FieldGeometry.cs"))

# Keys are the FieldGeometryOptions record field names, so the drift check can
# compare by name.
P = dict(
    FastMps=4.0,             # "definitely playing"; the fit set
    MinFitSamples=40,        # fewer fast samples than this: fit on everything
    ClusterCellM=10.0,       # main_cluster grid cell
    ClusterHalfCells=3,      # ... neighbourhood scored around each cell
    ClusterRadiusM=60.0,     # ... and kept around the winner
    DwellMinSec=20.0,        # line-up dwell: at least this long ...
    DwellMaxMeanMps=1.5,     # ... this slow on average (samples < 2x this) ...
    DwellFrac=0.55,          # ... beyond this fraction of the rough half-length
    BurstSec=20.0,           # a line-up is followed by a burst within this ...
    BurstMps=5.0,            # ... at least this fast
    LineupGapMinSec=90.0,    # line-ups closer than this are drill reps
    LineupGapMaxSec=480.0,   # a gap longer than this breaks the run
    LineupRunMin=3,          # line-ups needed to call gameplay started ...
    LineupSwitches=2,        # ... switching ends at least this often ...
    LineupDensity=1.2,       # ... with at most this many burst-dwells per run member
    LineQuantile=0.5,        # endzone line = this quantile of an end's dwell |along|
    WidthQuantile=0.03,      # width = the q .. 1-q span of fast cross
    EndzoneDepthM=18.0,      # box extends this far past each line
    SidePadM=3.0,            # sideline slack
    WinSec=20.0,             # rolling-median window for the box test
    SlowMaxMps=3.0,          # a sustained slow stretch has no sample above this ...
    SlowMinSec=150.0,        # ... for at least this long => off
    StintGapSec=30.0,        # on-field runs closer than this are one stint
    StintMinSec=45.0,        # stints shorter than this don't count
    SplitMinSec=90.0,        # a stint splits at a line-up at least this long after the last split
    RestMinSec=60.0,         # an off gap at least this long between stints is a rest period
)

# Lap labels for the lap-pressed fit exclusion (mirrors LapClassifierOptions).
LAP = dict(WarmupLapMinSec=900.0, PointLapMinAvgMps=0.95, PointLapMinMaxMps=5.0)


def _assert_matches_csharp():
    """Compare P against FieldGeometryOptions' defaults in the C#. Raises on any
    mismatch, or if the record can no longer be parsed - a silent skip would
    let the two drift unnoticed (as diagnose.py's FOLLOW_S did in 2026-08)."""
    with open(FIELD_GEOMETRY_CS) as f:
        cs = f.read()
    m = re.search(r"record FieldGeometryOptions\((.*?)\)\s*(?://[^\n]*)?\s*\n\{", cs, re.S)
    if not m:
        raise RuntimeError(f"FieldGeometryOptions record not found in {FIELD_GEOMETRY_CS}")
    found = dict(re.findall(r"(?:double|int)\s+(\w+)\s*=\s*([\d.]+)", m.group(1)))
    bad = []
    for k, v in P.items():
        if k not in found:
            bad.append(f"  {k}: missing from FieldGeometryOptions")
        elif float(found[k]) != float(v):
            bad.append(f"  {k} = {v} here but {found[k]} in C#")
    for k in found:
        if k not in P:
            bad.append(f"  {k}: in FieldGeometryOptions but not mirrored here")
    if bad:
        raise RuntimeError("fieldgeometry_v2.P has drifted from FieldGeometryOptions:\n" + "\n".join(bad))


try:
    _assert_matches_csharp()
except FileNotFoundError:
    print(f"warning: {FIELD_GEOMETRY_CS} not found, skipping constants-drift check", file=sys.stderr)
except RuntimeError as e:
    sys.exit(f"fieldgeometry_v2.py: {e}")


# --- primitives ---

def roll(t, vals, win, fn):
    """Two-pointer window over irregular timestamps (same as segment.py)."""
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


def haversine(lat1, lon1, lat2, lon2):
    r = 6371000.0
    p1, p2 = math.radians(lat1), math.radians(lat2)
    dp = math.radians(lat2 - lat1)
    dl = math.radians(lon2 - lon1)
    h = math.sin(dp / 2) ** 2 + math.cos(p1) * math.cos(p2) * math.sin(dl / 2) ** 2
    return 2 * r * math.asin(min(1.0, math.sqrt(h)))


def xy(lat, lon):
    mla, mlo = st.mean(lat), st.mean(lon)
    k = 111320.0
    return [((lo - mlo) * k * math.cos(math.radians(mla)), (la - mla) * k) for la, lo in zip(lat, lon)]


def frame(pts, ox, oy, th):
    c, s = math.cos(th), math.sin(th)
    return [((x - ox) * c + (y - oy) * s, -(x - ox) * s + (y - oy) * c) for x, y in pts]


def burst_after(t, spd, i1, include_self):
    """A >= BurstMps sample within BurstSec after sample i1."""
    k = i1 if include_self else i1 + 1
    while k < len(t) and t[k] - t[i1] <= P["BurstSec"]:
        if spd[k] >= P["BurstMps"]:
            return True
        k += 1
    return False


def dwells(t, along, spd, lo_abs):
    """Slow stretches beyond lo_abs along the axis: (i0, i1, side)."""
    out = []
    i = 0
    n = len(t)
    cap = P["DwellMaxMeanMps"] * 2
    while i < n:
        if abs(along[i]) > lo_abs:
            side = 1 if along[i] > 0 else -1
            j = i
            while j + 1 < n and abs(along[j + 1]) > lo_abs and (along[j + 1] > 0) == (side > 0):
                j += 1
            k = i
            while k <= j:
                if spd[k] < cap:
                    m = k
                    while m + 1 <= j and spd[m + 1] < cap:
                        m += 1
                    if t[m] - t[k] >= P["DwellMinSec"] and st.mean(spd[k:m + 1]) <= P["DwellMaxMeanMps"]:
                        out.append((k, m, side))
                    k = m + 1
                else:
                    k += 1
            i = j + 1
        else:
            i += 1
    return out


def gameplay_start(t, dw, spd):
    """Index of the first line-up of a run that looks like games, not drills: each
    line-up followed by a burst, consecutive ones LineupGapMin..Max apart (start to
    start), switching ends at least LineupSwitches times, and not dense with drill
    reps in between. 0 when no such run exists."""
    g = [x for x in dw if burst_after(t, spd, x[1], include_self=False)]
    for a in range(len(g)):
        run = [g[a]]
        for b in range(a + 1, len(g)):
            gap = t[g[b][0]] - t[run[-1][0]]
            if gap < P["LineupGapMinSec"]:
                continue
            if gap > P["LineupGapMaxSec"]:
                break
            run.append(g[b])
            sw = sum(1 for u, v in zip(run, run[1:]) if u[2] != v[2])
            if len(run) >= P["LineupRunMin"] and sw >= P["LineupSwitches"]:
                inside = [x for x in g if t[run[0][0]] <= t[x[0]] <= t[run[-1][0]]]
                if len(inside) <= len(run) * P["LineupDensity"]:
                    return g[a][0]
                break
    return 0


def main_cluster(pts, idx):
    """Members of idx near the place with the most fast running."""
    cell, half, radius = P["ClusterCellM"], int(P["ClusterHalfCells"]), P["ClusterRadiusM"]
    h = Counter((math.floor(pts[i][0] / cell), math.floor(pts[i][1] / cell)) for i in idx)
    best, best_score = None, -1
    for c in h:   # insertion order; first maximum wins
        score = sum(h.get((c[0] + a, c[1] + b), 0) for a in range(-half, half + 1) for b in range(-half, half + 1))
        if score > best_score:
            best, best_score = c, score
    bx, by = (best[0] + .5) * cell, (best[1] + .5) * cell
    near = [i for i in idx if math.hypot(pts[i][0] - bx, pts[i][1] - by) <= radius]
    mx, my = st.mean(pts[i][0] for i in near), st.mean(pts[i][1] for i in near)
    return [i for i in idx if math.hypot(pts[i][0] - mx, pts[i][1] - my) <= radius]


def pca(pts, idx):
    ox, oy = st.mean(pts[i][0] for i in idx), st.mean(pts[i][1] for i in idx)
    cxx = sum((pts[i][0] - ox) ** 2 for i in idx)
    cyy = sum((pts[i][1] - oy) ** 2 for i in idx)
    cxy = sum((pts[i][0] - ox) * (pts[i][1] - oy) for i in idx)
    return ox, oy, 0.5 * math.atan2(2 * cxy, cxx - cyy)


def rough_half_length(A, fit):
    fa = sorted(abs(A[i]) for i in fit)
    return fa[int(len(fa) * 0.9)]


def onfield(t, A, C, spd, halfw, line, g0):
    mc = roll(t, [abs(c) for c in C], P["WinSec"], st.median)
    ma = roll(t, A, P["WinSec"], st.median)
    lim = line + P["EndzoneDepthM"]
    inbox = [mc[i] < halfw + P["SidePadM"] and -lim < ma[i] < lim for i in range(len(t))]
    slow = [False] * len(t)
    i = 0
    while i < len(t):
        if spd[i] < P["SlowMaxMps"]:
            j = i
            while j + 1 < len(t) and spd[j + 1] < P["SlowMaxMps"]:
                j += 1
            if t[j] - t[i] >= P["SlowMinSec"]:
                for k in range(i, j + 1):
                    slow[k] = True
            i = j + 1
        else:
            i += 1
    on = [b and not s for b, s in zip(inbox, slow)]
    for i in range(g0):
        on[i] = False
    return on


def lap_is_point(dur, avg, mx):
    return dur <= LAP["WarmupLapMinSec"] and avg >= LAP["PointLapMinAvgMps"] and mx >= LAP["PointLapMinMaxMps"]


def fit_exclusion(t, laps):
    """laps: [(start_rel_s, duration_s, avg_mps, max_mps)]. Samples inside any lap
    that isn't a point lap (warmup, rest, ambiguous) are kept out of the fit.
    A lap with no speeds can't be labelled and excludes nothing."""
    ex = [False] * len(t)
    for ls, dur, avg, mx in laps:
        if avg is None or mx is None or lap_is_point(dur, avg, mx):
            continue
        for i in range(len(t)):
            if ls <= t[i] < ls + dur:
                ex[i] = True
    return ex


def analyse(t, lat, lon, spd, exclude=None):
    t = [float(x) for x in t]
    spd = [float(x or 0) for x in spd]
    n = len(t)
    pts = xy(lat, lon)
    ex = exclude or [False] * n
    fast = [i for i in range(n) if spd[i] >= P["FastMps"] and not ex[i]]
    if len(fast) < P["MinFitSamples"]:   # exclusion ate the fit: ignore it, then fall back to everything
        fast = [i for i in range(n) if spd[i] >= P["FastMps"]]
    if len(fast) < P["MinFitSamples"]:
        fast = list(range(n))
    fast = main_cluster(pts, fast)

    # fit -> gameplay start -> refit on post-start fast play, until the start settles
    fit = fast
    g0 = None
    for _ in range(4):
        ox, oy, th0 = pca(pts, fit)
        A = [a for a, _ in frame(pts, ox, oy, th0)]
        rh = rough_half_length(A, fit)
        ng = gameplay_start(t, dwells(t, A, spd, rh * P["DwellFrac"]), spd)
        if ng == g0:
            break
        g0 = ng
        nf = [i for i in fit if i >= g0]
        if len(nf) < P["MinFitSamples"]:
            break
        fit = nf

    after = [i for i in fast if i >= g0]
    fit = after if len(after) >= P["MinFitSamples"] else fast
    ox, oy, th = pca(pts, fit)
    A, C = (list(v) for v in zip(*frame(pts, ox, oy, th)))
    rh = rough_half_length(A, fit)
    dw2 = [x for x in dwells(t, A, spd, rh * P["DwellFrac"]) if x[0] >= g0]
    line = {}
    for side in (1, -1):
        v = sorted(abs(A[i]) for i0, i1, s_ in dw2 if s_ == side for i in range(i0, i1 + 1))
        line[side] = v[int(len(v) * P["LineQuantile"])] if len(v) > 10 else rh
    sh = (line[1] - line[-1]) / 2
    A = [x - sh for x in A]
    L = (line[1] + line[-1]) / 2
    fcs = sorted(C[i] for i in fit)
    lo = fcs[int(len(fcs) * P["WidthQuantile"])]
    hi = fcs[int(len(fcs) * (1 - P["WidthQuantile"]))]
    c0 = (lo + hi) / 2
    C = [c - c0 for c in C]
    halfw = (hi - lo) / 2

    on = onfield(t, A, C, spd, halfw, L, g0)

    runs = []
    i = g0
    while i < n:
        if on[i]:
            j = i
            while j + 1 < n and on[j + 1]:
                j += 1
            if runs and t[i] - t[runs[-1][1]] < P["StintGapSec"]:
                runs[-1][1] = j
            else:
                runs.append([i, j])
            i = j + 1
        else:
            i += 1
    stints = [r for r in runs if t[r[1]] - t[r[0]] >= P["StintMinSec"]]

    played = 0
    for a_, b_ in stints:
        last = t[a_]
        k = 1
        for i0, i1, _s in dw2:
            if a_ <= i0 <= b_ and t[i0] - last >= P["SplitMinSec"] and burst_after(t, spd, i1, include_self=True):
                k += 1
                last = t[i0]
        played += k
    rests = sum(1 for a, b in zip(stints, stints[1:]) if t[b[0]] - t[a[1]] >= P["RestMinSec"])

    # Live play: on-field time inside a stint, minus line-up dwells (waiting on the line).
    in_stint = [False] * n
    for a_, b_ in stints:
        for k in range(a_, b_ + 1):
            in_stint[k] = True
    for i0, i1, _s in dw2:
        for k in range(i0, i1 + 1):
            in_stint[k] = False
    on_s = on_d = live_s = live_d = 0.0
    for i in range(1, n):
        if not on[i - 1]:
            continue
        dt = t[i] - t[i - 1]
        dd = haversine(lat[i - 1], lon[i - 1], lat[i], lon[i])
        on_s += dt
        on_d += dd
        if in_stint[i - 1]:
            live_s += dt
            live_d += dd
    dur = t[-1] - t[0]
    return dict(on=on, g0=g0, th=th, halfw=halfw, halfl=L, played=played, rests=rests, stints=stints,
                onsec=on_s, offsec=dur - on_s, ondist=on_d, livesec=live_s, livedist=live_d, dur=dur)


def baseline(t, lat, lon, spd, exclude=None):
    """The baselines.json numbers FieldGeometryTests compares against."""
    r = analyse(t, lat, lon, spd, exclude)
    return {"onFieldSeconds": round(r["onsec"]), "durationSeconds": round(r["dur"]),
            "onFieldFraction": round(r["onsec"] / r["dur"], 4), "pointsPlayed": r["played"],
            "restPeriods": r["rests"], "gameplayStartSeconds": round(t[r["g0"]] - t[0]),
            "fieldWidthM": round(2 * r["halfw"], 1), "fieldLengthM": round(2 * r["halfl"], 1),
            "livePlaySeconds": round(r["livesec"]), "livePlayDistanceM": round(r["livedist"])}


def load_fixture(path):
    with gzip.open(path, "rt") as f:
        p = json.load(f)
    s = p["samples"]
    return p, [float(x) for x in s["t"]], s["lat"], s["lon"], s["spd"]


if __name__ == "__main__":
    for name in sorted(os.listdir(FIXTURES)):
        if name.endswith(".json.gz"):
            p, t, lat, lon, spd = load_fixture(os.path.join(FIXTURES, name))
            laps = [(l["startSec"], l["durationSec"], l["avgMps"], l["maxMps"]) for l in p.get("laps", [])]
            b = baseline(t, lat, lon, spd, fit_exclusion(t, laps) if laps else None)
            print(f"{name:20} {b}")
