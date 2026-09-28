#!/usr/bin/env python3
"""Held-out validation of the point-detector follow-filter settings.

Scores each candidate setting on the held-out fixtures (holdout.py): two games
per tournament that diagnose.py's sweep never sees, so agreement here is
evidence rather than a restatement of the fit. `--rotate TOURNAMENT` holds out
that whole tournament instead - does the setting survive an unseen field?

Reads the committed fixtures; no DB needed.

Usage:  python3 holdout_check.py [--rotate Regionals|BigC|Nationals]
"""
import sys

import diagnose as d
import holdout

# The shipped default is FOLLOW_S=60 / FRAC=0.50 (a93a610); diagnose.py pulls
# it live from FieldGeometry.cs and this row mirrors it. The rest are the
# comparison points from the 2026-08-27 relaxation sweep, kept for the record.
CANDIDATES = [
    ("current (FOLLOW_S=60, FRAC=0.50)", dict(follow_s=d.FOLLOW_S, follow_frac=d.FOLLOW_FRAC)),
    ("pre-a93a610 (FOLLOW_S=90, FRAC=0.60)", dict(follow_s=90, follow_frac=0.60)),
    ("FOLLOW_S=60, FRAC=0.40", dict(follow_s=60, follow_frac=0.40)),
    ("FOLLOW_S=45, FRAC=0.60", dict(follow_s=45, follow_frac=0.60)),
]


def main():
    rotate = sys.argv[sys.argv.index("--rotate") + 1] if "--rotate" in sys.argv else None
    base = {g["game"]: g for g in holdout.baselines()}
    held = sorted(holdout.heldout_games(base.values(), rotate))
    what = f"all of {rotate}" if rotate else "mixed split"
    print(f"Held-out set ({what}): games {held} - excluded from diagnose.py's sweep\n")

    results = {label: [] for label, _ in CANDIDATES}
    print(f"{'game':34} {'on%':>5} " + " ".join(f"{lab.split('(')[0].strip()[:14]:>14}" for lab, _ in CANDIDATES))
    for game in held:
        t, lat, lon, spd = d.load_fixture(holdout.fixture_path(game))
        r = d.analyse(t, lat, lon, spd)
        cells = []
        for label, kw in CANDIDATES:
            pts, _ = d.points_played(r["t"], r["along"], r["spd"], r["onfield"], r["halfl"], **kw)
            mpp = (r["on"] / 60 / pts) if pts else float("nan")
            results[label].append((pts, mpp))
            cells.append(f"{pts:>3}p {mpp:>4.1f}m/p")
        name = f"{game:02d} {base[game]['tournament']} {base[game]['name'] or ''}"[:32]
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
