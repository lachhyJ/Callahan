#!/usr/bin/env python3
"""Held-out validation of the point-detector follow-filter relaxation.

The FOLLOW_S/FOLLOW_FRAC sweep was tuned on the six April fixture games. This
scores the SAME candidate settings against the eleven Feb/Mar games (Regionals,
Big C), which were never used in that tuning — so agreement here is evidence
rather than a restatement of the fit.

Usage:  python3 holdout_check.py /path/to/callahan-ro.db
"""
import json
import os
import sqlite3
import sys

import diagnose as d

CANDIDATES = [
    ("current (FOLLOW_S=90, FRAC=0.60)", dict(follow_s=90, follow_frac=0.60)),
    ("FOLLOW_S=60, FRAC=0.50", dict(follow_s=60, follow_frac=0.50)),
    ("FOLLOW_S=60, FRAC=0.40", dict(follow_s=60, follow_frac=0.40)),
    ("FOLLOW_S=45, FRAC=0.60", dict(follow_s=45, follow_frac=0.60)),
]


def load_tracks(db_path, before="2026-04-01", after=None):
    db = sqlite3.connect(db_path)
    cols = {r[1] for r in db.execute("PRAGMA table_info(ActivityTracks)")}
    blob = "SamplesJson" if "SamplesJson" in cols else "Samples"
    q = ("SELECT a.Id, a.Date, a.Notes, a.OnFieldSeconds, a.PointsPlayed, t." + blob +
         " FROM Activities a JOIN ActivityTracks t ON t.ActivityId = a.Id "
         "WHERE a.Type = 1 AND a.PointsPlayed IS NOT NULL")
    if before:
        q += " AND a.Date < '" + before + "'"
    if after:
        q += " AND a.Date >= '" + after + "'"
    q += " ORDER BY a.Date, a.Id"
    out = []
    for aid, date, notes, onsec, pts, raw in db.execute(q):
        s = json.loads(raw)
        t = [float(x) for x in s["t"]]
        out.append((aid, date, notes, onsec, pts, t, s["lat"], s["lon"], s["spd"]))
    return out


def main():
    db_path = sys.argv[1]
    games = load_tracks(db_path)
    print("Held-out set: " + str(len(games)) + " Feb/Mar games (never used in tuning)\n")

    results = {label: [] for label, _ in CANDIDATES}
    print(f"{'game':34} {'on%':>5} " + " ".join(f"{lab.split('(')[0].strip()[:14]:>14}" for lab, _ in CANDIDATES))
    for aid, date, notes, onsec, stored_pts, t, lat, lon, spd in games:
        r = d.analyse(t, lat, lon, spd)
        cells = []
        for label, kw in CANDIDATES:
            pts, _ = d.points_played(r["t"], r["along"], r["spd"], r["onfield"], r["halfl"], **kw)
            mpp = (r["on"] / 60 / pts) if pts else float("nan")
            results[label].append((pts, mpp))
            cells.append(f"{pts:>3}p {mpp:>4.1f}m/p")
        name = (notes or str(aid))[:32]
        print(f"{name:34} {r['on']/r['dur']:>4.0%} " + " ".join(f"{c:>14}" for c in cells))

    print("\n--- summary (held-out) ---")
    for label, _ in CANDIDATES:
        vals = results[label]
        mpps = [m for _, m in vals if m == m]
        inband = sum(1 for m in mpps if 2.0 <= m <= 4.0)
        print(f"{label:34} total pts={sum(p for p,_ in vals):>4}  "
              f"min/pt range {min(mpps):.1f}-{max(mpps):.1f}  "
              f"in 2-4 min/pt band: {inband}/{len(mpps)}")


if __name__ == "__main__":
    main()
