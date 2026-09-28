"""The tune / held-out split over the committed FieldGeometry fixtures.

Any retune (diagnose.py's sweep) runs on the tune set only; holdout_check.py
scores candidates on the held-out set, so agreement there is evidence rather
than a restatement of the fit.

The split is a mix across tournaments - two games from each - because the
tournaments differ in field size (Big C was on a smaller oval), and a split by
tournament would test only one kind of field. The games were drawn once with
random.Random(SEED).sample per tournament, then frozen here: re-drawing on
every run would let the split drift as fixtures are added.

Fixture numbers follow date order (fixtures_from_db.py), so adding a new
tournament appends games and leaves these numbers alone. When one is added,
draw its two with the same seed (draw() below) and append them.

`--rotate TOURNAMENT` (on both scripts) is the other check: hold out that whole
tournament instead, to see whether a setting generalises to an unseen field.
"""
import json
import os
import random

SEED = 20260929
PER_TOURNAMENT = 2
HELDOUT = {2, 4, 9, 10, 15, 17}  # Regionals 2,4 · BigC 9,10 · Nationals 15,17

HERE = os.path.dirname(os.path.abspath(__file__))
FIXTURES = os.path.normpath(os.path.join(HERE, "..", "..", "tests", "Callahan.Api.Tests", "Fixtures"))


def baselines():
    with open(os.path.join(FIXTURES, "baselines.json")) as f:
        return json.load(f)["games"]


def draw(games):
    """Reproduce the seeded draw. Used to extend HELDOUT, not at run time."""
    by = {}
    for g in games:
        by.setdefault(g["tournament"], []).append(g["game"])
    rng = random.Random(SEED)
    return {k: sorted(rng.sample(v, PER_TOURNAMENT)) for k, v in sorted(by.items())}


def heldout_games(games, rotate=None):
    """Game numbers held out: the frozen mix, or every game of `rotate`."""
    if rotate is None:
        return set(HELDOUT)
    held = {g["game"] for g in games if g["tournament"] == rotate}
    if not held:
        raise SystemExit(f"no fixtures tagged {rotate!r}; tags are "
                         + ", ".join(sorted({g['tournament'] for g in games})))
    return held


def fixture_path(game):
    return os.path.join(FIXTURES, f"game-{game:02d}.json.gz")
