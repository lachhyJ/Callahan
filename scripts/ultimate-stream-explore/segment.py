#!/usr/bin/env python3
"""On/off-field segmentation + points-played estimate.

Two independent structures in the GPS, both confirmed against the athlete's
own recollection:

1. SIDELINE vs FIELD (geometry). Off a point he walks up and down the sideline
   - pinned to one lateral offset, moving along the field's long axis. On a
   point he uses the full width. So: rolling lateral SPREAD + distance from the
   centre line, not speed.

2. POINT BOUNDARIES (endzone dwells). Between points both teams reset to
   opposite endzones and there is a pull. That shows as a sustained stop at an
   extreme along-axis position, followed by a full-field traverse at speed.
   Counting those dwells inside on-field time counts points played.
"""
import json, math, statistics, sys

PATH = sys.argv[1] if len(sys.argv) > 1 else "tourney-stream.json"
WIN = 100.0
FAST = 4.0
MIN_DWELL = 75.0
EZ_FRAC = 0.55        # |along| beyond this fraction of half-length = endzone
EZ_MIN_S = 25.0       # a dwell must last this long to count as a reset
EZ_MAX_SPD = 2.5      # and be this slow on average
FOLLOW_S = 90.0       # after a dwell, look this far ahead...
FOLLOW_FRAC = 0.6     # ...and require this much of it to be on-field
# STRICT additionally requires a full-field pull traverse after the dwell. It
# under-counts badly (5.7 min/point vs a real ~2-4), so it is off by default -
# kept because it is the conservative floor on points played.
STRICT = "--strict" in sys.argv


def project(rows, D):
    pts, idxs = [], []
    for i, r in enumerate(rows):
        la, lo = r[D['directLatitude']], r[D['directLongitude']]
        if la is None or lo is None:
            continue
        pts.append((la, lo, r[D['directSpeed']] or 0)); idxs.append(i)
    mla = statistics.mean(p[0] for p in pts); mlo = statistics.mean(p[1] for p in pts)
    mlat, mlon = 111320.0, 111320.0 * math.cos(math.radians(mla))
    xy = [((p[1] - mlo) * mlon, (p[0] - mla) * mlat, p[2]) for p in pts]
    fast = [(x, y) for x, y, s in xy if s >= FAST]
    src = fast if len(fast) >= 40 else [(x, y) for x, y, _ in xy]
    mx = statistics.mean(p[0] for p in src); my = statistics.mean(p[1] for p in src)
    cxx = statistics.pvariance([p[0] for p in src]); cyy = statistics.pvariance([p[1] for p in src])
    cxy = sum((p[0] - mx) * (p[1] - my) for p in src) / len(src)
    th = 0.5 * math.atan2(2 * cxy, cxx - cyy); ct, st = math.cos(th), math.sin(th)
    along, cross = [], []
    for x, y, s in xy:
        dx, dy = x - mx, y - my
        along.append(dx * ct + dy * st); cross.append(-dx * st + dy * ct)
    fc = [c for c, (_, _, s) in zip(cross, xy) if s >= FAST]
    c0 = statistics.median(fc) if len(fc) >= 40 else statistics.median(cross)
    return idxs, along, [c - c0 for c in cross], [s for _, _, s in xy]


def roll(t, vals, win, fn):
    n = len(t); out = [0.0] * n; lo = hi = 0
    for i in range(n):
        while t[lo] < t[i] - win / 2: lo += 1
        if hi < lo: hi = lo
        while hi < n and t[hi] <= t[i] + win / 2: hi += 1
        out[i] = fn(vals[lo:hi]) if hi > lo else fn([vals[i]])
    return out


def spread(xs):
    if len(xs) < 4: return 0.0
    s = sorted(xs); n = len(s)
    return s[int(n * 0.9)] - s[int(n * 0.1)]


def merge(t, lab, mind):
    out = list(lab); i = 0
    while i < len(out):
        j = i
        while j < len(out) and out[j] == out[i]: j += 1
        if j > i and (t[j - 1] - t[i]) < mind and i > 0:
            for k in range(i, j): out[k] = out[i - 1]
            i = 0
        else: i = j
    return out


def runs(t, lab):
    r = []; i = 0
    while i < len(lab):
        j = i
        while j < len(lab) and lab[j] == lab[i]: j += 1
        r.append((lab[i], i, j - 1)); i = j
    return r


d = json.load(open(PATH))
tot_pts = 0
tot_on = tot_dur = 0.0
print(f"{'game':22} {'dur':>6} {'on-field':>9} {'%':>4} {'pts':>4} {'s/pt':>6}  timeline")
print("-" * 104)
detail = []
for g in d:
    D = {x['key']: x['metricsIndex'] for x in g['metricDescriptors']}
    rows = [r['metrics'] for r in g['activityDetailMetrics']]
    tall = [r[D['directTimestamp']] / 1000 for r in rows]; t0 = tall[0]
    idxs, along, cross, spd = project(rows, D)
    t = [tall[i] - t0 for i in idxs]

    fc = sorted(abs(c) for c, s in zip(cross, spd) if s >= FAST)
    halfw = fc[int(len(fc) * 0.9)] if len(fc) > 20 else 18.0
    fa = sorted(abs(a) for a, s in zip(along, spd) if s >= FAST)
    halfl = fa[int(len(fa) * 0.9)] if len(fa) > 20 else 45.0

    lat_spread = roll(t, cross, WIN, spread)
    abs_cross = roll(t, [abs(c) for c in cross], WIN, statistics.median)
    onfield = merge(t, [(ls > halfw * 0.8) or (ac < halfw * 0.55)
                        for ls, ac in zip(lat_spread, abs_cross)], MIN_DWELL)

    # Point boundary = an endzone reset FOLLOWED BY a pull traverse. The dwell
    # alone over-counts: play visits an endzone mid-point too (a turnover, a
    # score attempt). What only happens between points is both teams resetting
    # to opposite ends, so he then covers most of the field's length at speed.
    inez = [abs(a) > halfl * EZ_FRAC for a in along]
    pts = 0
    starts = []
    for state, i0, i1 in runs(t, inez):
        if not state:
            continue
        if t[i1] - t[i0] < EZ_MIN_S:
            continue
        if statistics.mean(spd[i0:i1 + 1]) > EZ_MAX_SPD:
            continue
        if sum(onfield[i0:i1 + 1]) < (i1 - i0 + 1) / 2:
            continue   # dwell happened while benched
        # Did he actually PLAY the point that followed? Look forward and
        # require the next stretch to be on-field. This is what separates a
        # real point start from sitting on the line for coach instructions and
        # then scurrying back to the sideline - and unlike a sprint test, it
        # still catches points started stationary (deep in a zone D, or as a
        # handler on offence), which is why the pull-traverse test under-counts.
        j = i1
        while j < len(t) - 1 and t[j] - t[i1] < FOLLOW_S:
            j += 1
        follow = onfield[i1:j + 1]
        if not follow or sum(follow) < len(follow) * FOLLOW_FRAC:
            continue

        if STRICT:
            seg_along = along[i1:j + 1]
            seg_spd = spd[i1:j + 1]
            if (max(seg_along) - min(seg_along)) < halfl or max(seg_spd, default=0) < 4.0:
                continue
        pts += 1
        starts.append(t[i1])

    on = sum(t[i] - t[i - 1] for i in range(1, len(t)) if onfield[i - 1])
    dur = t[-1] - t[0]
    tot_pts += pts; tot_on += on; tot_dur += dur
    tl = ''
    for m in range(int(dur // 60) + 1):
        seg = [onfield[i] for i in range(len(t)) if m * 60 <= t[i] < (m + 1) * 60]
        tl += ' ' if not seg else ('#' if sum(seg) >= len(seg) / 2 else '.')
    print(f"{g['activityName'][:22]:22} {dur/60:>5.0f}m {on/60:>8.0f}m {on/dur:>4.0%} "
          f"{pts:>4} {(on/pts if pts else 0):>5.0f}s  {tl[:40]}")
    gaps=[starts[i+1]-starts[i] for i in range(len(starts)-1)]
    detail.append((g['activityName'], tl, halfw, halfl, pts, on, dur, gaps))

print("-" * 104)
print(f"{'TOURNAMENT':22} {tot_dur/60:>5.0f}m {tot_on/60:>8.0f}m {tot_on/tot_dur:>4.0%} {tot_pts:>4}")
print()
for name, tl, halfw, halfl, pts, on, dur, gaps in detail:
    print(f"\n{name}  — field fitted {2*halfw:.0f}m wide x {2*halfl:.0f}m long | "
          f"{on/60:.0f}/{dur/60:.0f} min on-field | ~{pts} points")
    for k in range(0, len(tl), 60):
        print('   ' + tl[k:k + 60])
    if gaps: print('   gaps between point-starts (s): ' + ' '.join(f'{x:.0f}' for x in gaps))
