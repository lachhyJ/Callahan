#!/usr/bin/env python3
"""Regenerate the committed FieldGeometry test fixtures from a Callahan DB.

This supersedes make_fixtures.py (which only handled a raw Garmin
`--dump-stream`). The tracks now live in the app's own database in the exact
{t,lat,lon,spd} shape a PUT /api/activities/{id}/track stores, so the
fixtures are just a privacy-shifted copy of every Ultimate "Game" row that
has a track, plus segment.py's numbers as baselines.

Every tournament game so far is included, not one tournament's worth - the
classifier has to hold across WFDF-standard fields (Regionals, Nationals)
AND the smaller non-standard fields of club-run events (Big C, played on a
constrained oval). See the 2026-08-30 investigation.

Privacy: per-game longitude shift (-mean_lon) so no field location is in the
repo. Output-neutral - project() computes (lon - mean_lon), invariant under
a constant shift. Latitude (~-37.8, a temperate-southern band) is untouched.

    python3 fixtures_from_db.py /path/to/callahan.db
"""
import gzip
import json
import os
import sqlite3
import statistics
import sys

import make_fixtures as mf  # analyse() + the segment.py constants it mirrors

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.normpath(os.path.join(HERE, "..", "..", "tests", "Callahan.Api.Tests", "Fixtures"))

# TournamentId -> short tag used in baselines.json. Matches the Tournaments
# table seeded for the backfill (1 Regionals, 2 Big C, 3 Div 2 Nationals).
TOUR = {1: "Regionals", 2: "BigC", 3: "Nationals"}


def main():
    if len(sys.argv) < 2:
        sys.exit(__doc__)
    con = sqlite3.connect(sys.argv[1])
    rows = con.execute(
        "SELECT a.Id, a.Notes, a.TournamentId, t.StartEpochMs, t.MedianSpacingSec, t.SamplesJson "
        "FROM Activities a JOIN ActivityTracks t ON t.ActivityId = a.Id "
        "JOIN ActivitySessionTypes st ON st.Id = a.ActivitySessionTypeId "
        "WHERE a.Type = 1 AND st.Name = 'Game' AND a.DeletedAt IS NULL "
        "ORDER BY a.Date, a.Id"
    ).fetchall()
    if not rows:
        sys.exit("no Ultimate 'Game' activities with tracks in that DB")

    os.makedirs(OUT, exist_ok=True)
    baselines = []
    agg = {}
    for gi, (aid, notes, tid, start_ms, spacing, raw) in enumerate(rows, 1):
        s = json.loads(raw)
        t = [int(round(float(x))) for x in s["t"]]
        lat = [float(x) for x in s["lat"]]
        lon = [float(x) for x in s["lon"]]
        spd = [float(x) for x in s["spd"]]

        base = mf.analyse([float(x) for x in t], lat, lon, spd)
        tag = TOUR.get(tid, "Other")
        base["game"] = gi
        base["name"] = notes
        base["tournament"] = tag
        baselines.append(base)

        a = agg.setdefault(tag, dict(onFieldSeconds=0, durationSeconds=0, pointsPlayed=0,
                                     livePlaySeconds=0, livePlayDistanceM=0))
        for k in a:
            a[k] += base[k]

        clon = statistics.mean(lon)
        payload = {
            "startEpochMs": int(start_ms),
            "sampleCount": len(t),
            "medianSpacingSec": round(float(spacing), 2) if spacing is not None else None,
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
        print(f"game-{gi:02d}.json.gz  {os.path.getsize(path):>6} B  {tag:9} "
              f"{base['name'][:24]:24}  {base['onFieldSeconds']/60:.0f}/{base['durationSeconds']/60:.0f} min  "
              f"{base['pointsPlayed']} pts  {base['fieldWidthM']:.0f}x{base['fieldLengthM']:.0f} m")

    for tag, a in agg.items():
        a["onFieldFraction"] = round(a["onFieldSeconds"] / a["durationSeconds"], 4)

    # Stale game-NN files from a previous run with more games.
    for f in os.listdir(OUT):
        if f.startswith("game-") and f.endswith(".json.gz"):
            n = int(f[5:7])
            if n > len(rows):
                os.remove(os.path.join(OUT, f))
                print(f"removed stale {f}")

    with open(os.path.join(OUT, "baselines.json"), "w") as f:
        json.dump({"games": baselines, "tournaments": agg}, f, indent=2)
        f.write("\n")
    print(f"\n{len(rows)} games, tournaments: " +
          ", ".join(f"{k} {v['pointsPlayed']}pts" for k, v in agg.items()))


if __name__ == "__main__":
    main()
