#!/usr/bin/env python3
"""Turn a `--dump-stream` JSON into committed test fixtures for FieldGeometry.

For refreshing the committed fixtures from production, use
fixtures_from_db.py instead - it reads the Callahan DB (where the tracks now
live in this exact shape), covers every tournament game, and tags each by
tournament. This script only handles a raw Garmin `--dump-stream` and writes
a flat single-aggregate baselines.json; keep it for a stream that isn't in
the DB yet. Its analyse() is imported by fixtures_from_db.py.

Input:  a `garmin_sync.py --dump-stream` array (raw get_activity_details).
Output: tests/Callahan.Api.Tests/Fixtures/game-0N.json.gz  (the exact wire /
        storage shape a PUT /api/activities/{id}/track will carry)
        tests/Callahan.Api.Tests/Fixtures/baselines.json    (segment.py numbers)

Privacy: each game's longitudes are shifted by a per-game constant (-mean_lon)
so the values centre on 0. This is EXACTLY output-neutral - project() computes
`(lon - mean_lon)`, invariant under a constant shift - and the latitude (and
therefore the cos(lat) metres scale) is left untouched. Latitude ~ -37.8 stays
real; on its own it's a temperate-southern band, not a location.

    python3 make_fixtures.py /path/to/tourney-stream.json
"""
import gzip
import json
import math
import os
import statistics
import sys

import diagnose as d  # the classifier primitives + constants, guarded against FieldGeometry.cs

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.normpath(os.path.join(HERE, "..", "..", "tests", "Callahan.Api.Tests", "Fixtures"))


def haversine(lat1, lon1, lat2, lon2):
    r = 6371000.0
    p1, p2 = math.radians(lat1), math.radians(lat2)
    dp = math.radians(lat2 - lat1)
    dl = math.radians(lon2 - lon1)
    h = math.sin(dp / 2) ** 2 + math.cos(p1) * math.cos(p2) * math.sin(dl / 2) ** 2
    return 2 * r * math.asin(min(1.0, math.sqrt(h)))


def analyse(t, lat, lon, spd):
    """The baselines.json numbers FieldGeometryTests compares against.

    On-field labelling and the field fit come from diagnose.analyse(); only the
    dwell list (needed for live play, which diagnose doesn't report) is walked
    here, with the same acceptance rules as diagnose.points_played()."""
    r = d.analyse(t, lat, lon, spd)
    along, onfield, halfw, halfl = r["along"], r["onfield"], r["halfw"], r["halfl"]

    inez = [abs(a) > halfl * d.EZ_FRAC for a in along]
    dwells = []
    for state, i0, i1 in d.runs(inez):
        if not state or t[i1] - t[i0] < d.EZ_MIN_S:
            continue
        if statistics.mean(spd[i0:i1 + 1]) > d.EZ_MAX_SPD:
            continue
        if sum(onfield[i0:i1 + 1]) < (i1 - i0 + 1) / 2:
            continue
        j = i1
        while j < len(t) - 1 and t[j] - t[i1] < d.FOLLOW_S:
            j += 1
        follow = onfield[i1:j + 1]
        if not follow or sum(follow) < len(follow) * d.FOLLOW_FRAC:
            continue
        dwells.append((i0, i1))
    pts = len(dwells)
    assert pts == r["pts"], "dwell walk disagrees with diagnose.points_played"

    on = r["on"]
    # Live play (segment.py b2): time + GPS distance over the on-field windows
    # between consecutive accepted dwells. Matches GeometryResult.LivePlay*.
    live = sum(t[i] - t[i - 1]
               for k in range(len(dwells) - 1)
               for i in range(dwells[k][1] + 1, dwells[k + 1][0] + 1)
               if onfield[i - 1])
    live_dist = sum(haversine(lat[i - 1], lon[i - 1], lat[i], lon[i])
                    for k in range(len(dwells) - 1)
                    for i in range(dwells[k][1] + 1, dwells[k + 1][0] + 1)
                    if onfield[i - 1])
    dur = t[-1] - t[0]
    return {"onFieldSeconds": round(on), "durationSeconds": round(dur),
            "onFieldFraction": round(on / dur, 4), "pointsPlayed": pts,
            "fieldWidthM": round(2 * halfw, 1), "fieldLengthM": round(2 * halfl, 1),
            "livePlaySeconds": round(live), "livePlayDistanceM": round(live_dist)}


def main():
    if len(sys.argv) < 2:
        sys.exit(__doc__)
    games = json.load(open(sys.argv[1]))
    os.makedirs(OUT, exist_ok=True)
    baselines = []
    tot_on = tot_dur = tot_pts = tot_live = tot_live_dist = 0
    for gi, g in enumerate(games, 1):
        D = {x["key"]: x["metricsIndex"] for x in g["metricDescriptors"]}
        rows = [r["metrics"] for r in g["activityDetailMetrics"]]
        keep = [r for r in rows if r[D["directLatitude"]] is not None
                and r[D["directLongitude"]] is not None]
        ts = [r[D["directTimestamp"]] for r in keep]            # epoch ms (float)
        t0 = int(round(ts[0]))
        t = [round((x - t0) / 1000) for x in ts]                # int seconds rel
        lat = [r[D["directLatitude"]] for r in keep]
        lon = [r[D["directLongitude"]] for r in keep]
        spd = [r[D["directSpeed"]] or 0.0 for r in keep]

        base = analyse([float(x) for x in t], lat, lon, spd)
        base["game"] = gi
        base["name"] = g.get("activityName")
        baselines.append(base)
        tot_on += base["onFieldSeconds"]
        tot_dur += base["durationSeconds"]
        tot_pts += base["pointsPlayed"]
        tot_live += base["livePlaySeconds"]
        tot_live_dist += base["livePlayDistanceM"]

        # privacy shift: centre longitudes on 0 (per-game constant, output-neutral)
        clon = statistics.mean(lon)
        payload = {
            "startEpochMs": t0,
            "sampleCount": len(keep),
            "medianSpacingSec": round(statistics.median(
                [t[i + 1] - t[i] for i in range(len(t) - 1)]), 2),
            "samples": {
                "t": t,
                "lat": [round(v, 6) for v in lat],
                "lon": [round(v - clon, 6) for v in lon],
                "spd": [round(v, 2) for v in spd],
            },
        }
        path = os.path.join(OUT, f"game-{gi:02d}.json.gz")
        with gzip.open(path, "wt") as f:
            json.dump(payload, f, separators=(",", ":"))
        print(f"game-{gi:02d}.json.gz  {os.path.getsize(path):>6} B  "
              f"{base['onFieldSeconds']/60:.0f}/{base['durationSeconds']/60:.0f} min  "
              f"{base['pointsPlayed']} pts  {base['fieldWidthM']:.0f}x{base['fieldLengthM']:.0f} m")

    summary = {"games": baselines,
               "tournament": {"onFieldSeconds": tot_on, "durationSeconds": tot_dur,
                              "onFieldFraction": round(tot_on / tot_dur, 4),
                              "pointsPlayed": tot_pts,
                              "livePlaySeconds": tot_live,
                              "livePlayDistanceM": tot_live_dist}}
    with open(os.path.join(OUT, "baselines.json"), "w") as f:
        json.dump(summary, f, indent=2)
    print(f"\ntournament: {tot_on/60:.0f}/{tot_dur/60:.0f} min "
          f"({tot_on/tot_dur:.0%}), {tot_pts} points")


if __name__ == "__main__":
    main()
