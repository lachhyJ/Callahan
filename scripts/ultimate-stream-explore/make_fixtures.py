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

Privacy: see anonymise.py - per-game lat/lon shift (cos(lat)-compensated) and a
per-run startEpochMs shift. The baselines are computed on the shifted values, so
they describe exactly what is committed.

    python3 make_fixtures.py /path/to/tourney-stream.json
"""
import gzip
import json
import os
import statistics
import sys

import anonymise as anon
import fieldgeometry_v2 as v2  # the detector's reference; guarded against FieldGeometry.cs

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.normpath(os.path.join(HERE, "..", "..", "tests", "Callahan.Api.Tests", "Fixtures"))


def analyse(t, lat, lon, spd, exclude=None):
    """The baselines.json numbers FieldGeometryTests compares against: the v5
    detector's reference implementation (fieldgeometry_v2.baseline)."""
    return v2.baseline(t, lat, lon, spd, exclude)


def main():
    if len(sys.argv) < 2:
        sys.exit(__doc__)
    games = json.load(open(sys.argv[1]))
    os.makedirs(OUT, exist_ok=True)
    epoch_shift = anon.epoch_shift_ms()
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

        lat, lon = anon.blur_track(lat, lon)
        base = analyse([float(x) for x in t], lat, lon, spd)
        base["game"] = gi
        base["name"] = g.get("activityName")
        baselines.append(base)
        tot_on += base["onFieldSeconds"]
        tot_dur += base["durationSeconds"]
        tot_pts += base["pointsPlayed"]
        tot_live += base["livePlaySeconds"]
        tot_live_dist += base["livePlayDistanceM"]

        payload = {
            "startEpochMs": t0 + epoch_shift,
            "sampleCount": len(keep),
            "medianSpacingSec": round(statistics.median(
                [t[i + 1] - t[i] for i in range(len(t) - 1)]), 2),
            "samples": {
                "t": t,
                "lat": lat,
                "lon": lon,
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
