#!/usr/bin/env python3
"""Tests for the "warmup recorded?" gameplay-start override in fieldgeometry_v2.py.
Mirrors the C# FieldGeometryWarmupTests branch for branch.

    python3 -m unittest test_fieldgeometry_v2      # from this directory
"""
import os
import unittest

import fieldgeometry_v2 as g

WARM = g.LAP["WarmupLapMinSec"]


def lap(start, dur, avg=1.2, mx=6.0):
    return (start, dur, avg, mx)


def fixture(name):
    p, t, lat, lon, spd = g.load_fixture(os.path.join(g.FIXTURES, name + ".json.gz"))
    laps = [(l["startSec"], l["durationSec"], l["avgMps"], l["maxMps"]) for l in p.get("laps", [])]
    return t, lat, lon, spd, laps


class KnownStart(unittest.TestCase):
    def test_no_pins_start_to_zero(self):
        self.assertEqual(g.known_start([lap(0, WARM + 300), lap(WARM + 300, 120)], False), 0.0)

    def test_dont_know_infers(self):
        self.assertIsNone(g.known_start([lap(0, WARM + 300), lap(WARM + 300, 120)], None))

    def test_yes_with_warmup_lap_is_its_end(self):
        self.assertEqual(g.known_start([lap(0, 1200), lap(1200, 120), lap(1320, 90)], True), 1200)

    def test_yes_without_warmup_lap_infers(self):
        self.assertIsNone(g.known_start([lap(0, 300), lap(300, 120)], True))
        self.assertIsNone(g.known_start([], True))

    def test_warmup_lap_found_by_duration_not_position(self):
        # Lap 1 is sideline time before the warmup: the warmup is lap 2.
        self.assertEqual(g.known_start([lap(0, 200, 0.3, 2.0), lap(200, 1500), lap(1700, 120)], True), 1700)
        # List order doesn't matter either, only start time.
        self.assertEqual(g.known_start([lap(1700, 120), lap(0, 200, 0.3, 2.0), lap(200, 1500)], True), 1700)

    def test_earliest_warmup_lap_wins(self):
        self.assertEqual(g.known_start([lap(0, 1000), lap(1000, 120), lap(1120, 1200), lap(2320, 90)], True), 1000)

    def test_single_long_lap_is_not_a_press(self):
        # Garmin's one default lap over the whole session.
        self.assertIsNone(g.known_start([lap(0, 5400)], True))
        # ... or a long last lap with nothing after it.
        self.assertIsNone(g.known_start([lap(0, 300), lap(300, 5000)], True))


class AnalyseWithStart(unittest.TestCase):
    def test_none_is_unchanged(self):
        t, lat, lon, spd, _ = fixture("game-05")
        self.assertEqual(g.baseline(t, lat, lon, spd), g.baseline(t, lat, lon, spd, start=None))

    def test_no_fixes_game_15s_late_start(self):
        # Colossus: inferred start ~59 min, off-field for the first hour.
        t, lat, lon, spd, _ = fixture("game-15")
        inferred = g.baseline(t, lat, lon, spd)
        pinned = g.baseline(t, lat, lon, spd, start=g.known_start([], False))
        self.assertGreater(inferred["gameplayStartSeconds"], 3000)
        self.assertEqual(pinned["gameplayStartSeconds"], 0)
        self.assertGreater(pinned["onFieldSeconds"], inferred["onFieldSeconds"] + 1000)
        self.assertGreater(pinned["pointsPlayed"], inferred["pointsPlayed"])

    def test_yes_with_warmup_lap_fixes_chilly(self):
        # Chilly #3: inference finds no start (line-ups mostly at one end).
        t, lat, lon, spd, laps = fixture("tryout-03")
        ex = g.fit_exclusion(t, laps)
        self.assertEqual(g.baseline(t, lat, lon, spd, ex)["gameplayStartSeconds"], 0)
        start = g.known_start(laps, True)
        b = g.baseline(t, lat, lon, spd, ex, start)
        self.assertAlmostEqual(b["gameplayStartSeconds"], start, delta=2)
        self.assertGreater(start, 1200)
        # The laps say 15 points, 13 rest periods.
        self.assertEqual((b["pointsPlayed"], b["restPeriods"]), (15, 13))

    def test_nothing_on_field_before_known_start(self):
        t, lat, lon, spd, laps = fixture("tryout-01")
        start = g.known_start(laps, True)
        r = g.analyse(t, lat, lon, spd, g.fit_exclusion(t, laps), start)
        self.assertFalse(any(on for ti, on in zip(t, r["on"]) if ti < start))
        self.assertTrue(any(r["on"]))


if __name__ == "__main__":
    unittest.main()
