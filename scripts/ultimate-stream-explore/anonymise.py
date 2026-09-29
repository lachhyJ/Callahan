#!/usr/bin/env python3
"""Location/date blurring for the committed FieldGeometry test fixtures.

Per game: longitude is centred on 0 and latitude is moved to a random band far
from the real one, with the longitude offsets rescaled by cos(old)/cos(new) so
FieldGeometry.project()'s metres-per-degree (it uses cos of the mean latitude)
gives the same local geometry. Per run: startEpochMs moves back by one random
whole number of weeks, shared by every game, so game order, gaps between games,
time-of-day and day-of-week are preserved but the calendar date is not.

The random draws are deliberately not seeded and not recorded anywhere, so the
committed values can't be inverted by re-running this script.

Re-blur fixtures already in the repo (no DB needed):
    python3 anonymise.py ../../tests/Callahan.Api.Tests/Fixtures
"""
import gzip
import json
import math
import os
import random
import statistics
import sys

_rng = random.SystemRandom()
WEEK_MS = 7 * 24 * 3600 * 1000
# 6 dp (~0.1 m) re-rounding is NOT neutral: it flips threshold decisions in the
# classifier (one game lost a point). 9 dp reproduces every baseline exactly.
DP = 9


def epoch_shift_ms():
    """One shift per run: 3-8 years back, a whole number of weeks."""
    return -_rng.randint(3 * 52, 8 * 52) * WEEK_MS


def _new_mean_lat():
    # Never within 5 degrees of anywhere Australian; sign is irrelevant to the maths.
    return _rng.choice([_rng.uniform(-75, -50), _rng.uniform(8, 55)])


def blur_track(lat, lon):
    """Return (lat, lon) shifted for one game; local geometry preserved."""
    mla, mlo = statistics.mean(lat), statistics.mean(lon)
    new_mla = _new_mean_lat()
    k = math.cos(math.radians(mla)) / math.cos(math.radians(new_mla))
    return ([round(v - mla + new_mla, DP) for v in lat],
            [round((v - mlo) * k, DP) for v in lon])


def _main(fixtures_dir):
    shift = epoch_shift_ms()
    for name in sorted(os.listdir(fixtures_dir)):
        if not (name.startswith("game-") and name.endswith(".json.gz")):
            continue
        path = os.path.join(fixtures_dir, name)
        with gzip.open(path, "rt") as f:
            p = json.load(f)
        s = p["samples"]
        s["lat"], s["lon"] = blur_track(s["lat"], s["lon"])
        p["startEpochMs"] += shift
        with gzip.open(path, "wt") as f:
            json.dump(p, f, separators=(",", ":"))
        print(f"{name}: mean lat {statistics.mean(s['lat']):.1f}, start {p['startEpochMs']}")


if __name__ == "__main__":
    if len(sys.argv) != 2:
        sys.exit(__doc__)
    _main(sys.argv[1])
