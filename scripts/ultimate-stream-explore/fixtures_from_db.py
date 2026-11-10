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

Privacy: see anonymise.py - per-game lat/lon shift (cos(lat)-compensated) and a
per-run startEpochMs shift, applied before the baselines are computed so they
describe exactly what is committed.

    python3 fixtures_from_db.py /path/to/callahan.db
    python3 fixtures_from_db.py /path/to/callahan.db --tryouts 53,55,54,56
    python3 fixtures_from_db.py --rebaseline

--tryouts writes lap-pressed sessions as tryout-NN.json.gz (same payload plus
their laps, relative to the track start) and tryouts.json (the reference
detector's numbers, with the laps' fit exclusion). These are the ground truth
for rest detection: FieldGeometryTryoutTests scores on-field against the laps.

--rebaseline recomputes baselines.json and tryouts.json from the committed
fixtures (no DB, no re-blur) - what a detector change needs.
"""
import datetime as dt
import gzip
import json
import os
import sqlite3
import statistics
import sys

import anonymise as anon
import fieldgeometry_v2 as v2
import make_fixtures as mf  # analyse() = the detector's reference baseline

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.normpath(os.path.join(HERE, "..", "..", "tests", "Callahan.Api.Tests", "Fixtures"))

# TournamentId -> short tag used in baselines.json. Matches the Tournaments
# table seeded for the backfill (1 Regionals, 2 Big C, 3 Div 2 Nationals).
TOUR = {1: "Regionals", 2: "BigC", 3: "Nationals"}


def lap_label(dur, avg, mx):
    """The 2026-11-06 tryout labels: warmup / point / rest / amb."""
    if dur > v2.LAP["WarmupLapMinSec"]:
        return "warmup"
    if v2.lap_is_point(dur, avg, mx):
        return "point"
    if avg <= 0.6 and mx < 5.0:
        return "rest"
    return "amb"


def lap_counts(laps):
    """Point laps, and rest periods = runs of rest/ambiguous laps between point laps."""
    code = "".join({"point": "P", "rest": "r", "amb": "a", "warmup": "W"}[lap_label(l["durationSec"], l["avgMps"], l["maxMps"])] for l in laps)
    runs = 0
    for k, ch in enumerate(code):
        if ch in "ra" and (k == 0 or code[k - 1] not in "ra"):
            j = k
            while j < len(code) and code[j] in "ra":
                j += 1
            if k > 0 and code[k - 1] == "P" and j < len(code) and code[j] == "P":
                runs += 1
    return code.count("P"), runs


def tryout_baseline(p):
    s = p["samples"]
    t = [float(x) for x in s["t"]]
    laps = p["laps"]
    ex = v2.fit_exclusion(t, [(l["startSec"], l["durationSec"], l["avgMps"], l["maxMps"]) for l in laps])
    b = mf.analyse(t, s["lat"], s["lon"], s["spd"], ex)
    b["lapPoints"], b["lapRestPeriods"] = lap_counts(laps)
    return b


def write_tryouts(con, ids):
    epoch_shift = anon.epoch_shift_ms()
    out = []
    for k, aid in enumerate(ids, 1):
        notes, = con.execute("SELECT Notes FROM Activities WHERE Id=?", (aid,)).fetchone()
        start_ms, spacing, raw = con.execute(
            "SELECT StartEpochMs, MedianSpacingSec, SamplesJson FROM ActivityTracks WHERE ActivityId=?", (aid,)).fetchone()
        s = json.loads(raw)
        t = [int(round(float(x))) for x in s["t"]]
        lat, lon = anon.blur_track([float(x) for x in s["lat"]], [float(x) for x in s["lon"]])
        laps = []
        for stt, dur, avg, mx in con.execute(
                "SELECT StartTimeGmt, DurationSeconds, AvgSpeedMps, MaxSpeedMps FROM ActivityLaps "
                "WHERE ActivityId=? ORDER BY LapIndex", (aid,)):
            ls = (dt.datetime.fromisoformat(stt).replace(tzinfo=dt.timezone.utc).timestamp() * 1000 - start_ms) / 1000
            laps.append({"startSec": round(ls, 3), "durationSec": float(dur), "avgMps": float(avg), "maxMps": float(mx)})
        payload = {
            "startEpochMs": int(start_ms) + epoch_shift,
            "sampleCount": len(t),
            "medianSpacingSec": round(float(spacing), 2) if spacing is not None else None,
            "samples": {"t": t, "lat": lat, "lon": lon, "spd": [round(float(v or 0), 2) for v in s["spd"]]},
            "laps": laps,
        }
        path = os.path.join(OUT, f"tryout-{k:02d}.json.gz")
        with gzip.open(path, "wt") as f:
            json.dump(payload, f, separators=(",", ":"))
        b = tryout_baseline(payload)
        b["tryout"], b["name"] = k, notes
        out.append(b)
        print(f"tryout-{k:02d}.json.gz  {notes:20} {b}")
    with open(os.path.join(OUT, "tryouts.json"), "w") as f:
        json.dump({"tryouts": out}, f, indent=2)
        f.write("\n")


def rebaseline():
    """Recompute baselines.json / tryouts.json from the committed fixtures."""
    path = os.path.join(OUT, "baselines.json")
    old = json.load(open(path))
    games, agg = [], {}
    for g in old["games"]:
        p, t, lat, lon, spd = v2.load_fixture(os.path.join(OUT, f"game-{g['game']:02d}.json.gz"))
        b = mf.analyse(t, lat, lon, spd)
        b.update(game=g["game"], name=g["name"], tournament=g["tournament"])
        games.append(b)
        a = agg.setdefault(g["tournament"], dict(onFieldSeconds=0, durationSeconds=0, pointsPlayed=0,
                                                 restPeriods=0, livePlaySeconds=0, livePlayDistanceM=0))
        for k in a:
            a[k] += b[k]
    for a in agg.values():
        a["onFieldFraction"] = round(a["onFieldSeconds"] / a["durationSeconds"], 4)
    with open(path, "w") as f:
        json.dump({"games": games, "tournaments": agg}, f, indent=2)
        f.write("\n")
    tpath = os.path.join(OUT, "tryouts.json")
    if os.path.exists(tpath):
        out = []
        for old_t in json.load(open(tpath))["tryouts"]:
            with gzip.open(os.path.join(OUT, f"tryout-{old_t['tryout']:02d}.json.gz"), "rt") as f:
                b = tryout_baseline(json.load(f))
            b["tryout"], b["name"] = old_t["tryout"], old_t["name"]
            out.append(b)
        with open(tpath, "w") as f:
            json.dump({"tryouts": out}, f, indent=2)
            f.write("\n")
    for b in games:
        print(f"game-{b['game']:02d} {b['name'][:26]:26} {b['pointsPlayed']:>3} pts {b['restPeriods']:>3} rests "
              f"{b['onFieldFraction']:.0%} on  {b['fieldWidthM']:.0f}x{b['fieldLengthM']:.0f} m  start {b['gameplayStartSeconds']/60:.0f} min")


def main():
    if "--rebaseline" in sys.argv:
        return rebaseline()
    if len(sys.argv) < 2:
        sys.exit(__doc__)
    con = sqlite3.connect(sys.argv[1])
    if "--tryouts" in sys.argv:
        return write_tryouts(con, [int(x) for x in sys.argv[sys.argv.index("--tryouts") + 1].split(",")])
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
    epoch_shift = anon.epoch_shift_ms()
    baselines = []
    agg = {}
    for gi, (aid, notes, tid, start_ms, spacing, raw) in enumerate(rows, 1):
        s = json.loads(raw)
        t = [int(round(float(x))) for x in s["t"]]
        lat = [float(x) for x in s["lat"]]
        lon = [float(x) for x in s["lon"]]
        spd = [float(x) for x in s["spd"]]

        lat, lon = anon.blur_track(lat, lon)
        base = mf.analyse([float(x) for x in t], lat, lon, spd)
        tag = TOUR.get(tid, "Other")
        base["game"] = gi
        base["name"] = notes
        base["tournament"] = tag
        baselines.append(base)

        a = agg.setdefault(tag, dict(onFieldSeconds=0, durationSeconds=0, pointsPlayed=0,
                                     restPeriods=0, livePlaySeconds=0, livePlayDistanceM=0))
        for k in a:
            a[k] += base[k]

        payload = {
            "startEpochMs": int(start_ms) + epoch_shift,
            "sampleCount": len(t),
            "medianSpacingSec": round(float(spacing), 2) if spacing is not None else None,
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
