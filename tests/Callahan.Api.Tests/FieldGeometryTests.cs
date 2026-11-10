using Callahan.Api.Services;

namespace Callahan.Api.Tests;

// Oracle: scripts/ultimate-stream-explore/fieldgeometry_v2.py (the detector's
// reference implementation) on every tournament game synced so far - 6
// Regionals + 5 Big C + 6 Div 2 Nationals. Big C was played on a constrained
// club oval (shorter fields), so this set spans field regimes deliberately: a
// geometry change that only holds on WFDF-standard sizes fails here. These
// games have no lap presses, so this is agreement with the reference, not
// ground truth - FieldGeometryTryoutTests carries that. Tight bands rather
// than equality: summation order and Atan2 won't reproduce CPython
// bit-for-bit.
public class FieldGeometryTests
{
    // Every game in baselines.json, not a hard-coded count: fixtures_from_db.py
    // regenerates however many tournament games exist.
    public static IEnumerable<object[]> Games =>
        TestFixtures.LoadBaselines().Games.Select(b => new object[] { b.Game });

    [Fact]
    public void EveryBaselineHasATrackFixture_AndViceVersa()
    {
        var embedded = typeof(TestFixtures).Assembly.GetManifestResourceNames()
            .Count(n => n.Contains(".game-") && n.EndsWith(".json.gz"));
        Assert.Equal(TestFixtures.LoadBaselines().Games.Count, embedded);
    }

    public static IEnumerable<object[]> Tournaments =>
        new[] { "Regionals", "BigC", "Nationals" }.Select(t => new object[] { t });

    [Theory]
    [MemberData(nameof(Games))]
    public void MatchesReferenceBaseline(int game)
    {
        var baseline = TestFixtures.LoadBaselines().Games.Single(b => b.Game == game);
        var (_, samples) = TestFixtures.LoadTrack(game);

        var r = FieldGeometry.Analyse(samples);

        double frac = (double)r.OnFieldSeconds / (r.OnFieldSeconds + r.OffFieldSeconds);
        Assert.InRange(frac, baseline.OnFieldFraction - 0.03, baseline.OnFieldFraction + 0.03);
        Assert.InRange(r.PointsPlayed, baseline.PointsPlayed - 1, baseline.PointsPlayed + 1);
        Assert.InRange(r.RestPeriods, baseline.RestPeriods - 1, baseline.RestPeriods + 1);
        Assert.InRange(r.GameplayStartT, baseline.GameplayStartSeconds - 5, baseline.GameplayStartSeconds + 5);

        // Live play (on-field time inside a stint, off the line) tracks the
        // reference within a band, and can never exceed total on-field time.
        Assert.InRange(r.LivePlaySeconds, baseline.LivePlaySeconds * 0.90, baseline.LivePlaySeconds * 1.10);
        Assert.True(r.LivePlaySeconds <= r.OnFieldSeconds);

        // Live-play distance is summed over the same windows: bands against the
        // fixture, and never more than total on-field distance.
        Assert.InRange(r.LivePlayDistanceM, baseline.LivePlayDistanceM * 0.90, baseline.LivePlayDistanceM * 1.10);
        Assert.True(r.LivePlayDistanceM <= r.OnFieldDistanceM + 1.0);

        int dur = r.OnFieldSeconds + r.OffFieldSeconds;
        Assert.InRange(dur, (int)(baseline.DurationSeconds * 0.98), (int)(baseline.DurationSeconds * 1.02));

        Assert.InRange(2 * r.Fit.HalfWidthM, baseline.FieldWidthM - 1.0, baseline.FieldWidthM + 1.0);
        Assert.InRange(2 * r.Fit.HalfLengthM, baseline.FieldLengthM - 1.0, baseline.FieldLengthM + 1.0);

        // Still recognisably a field. Wide band on purpose: the fit spans club
        // ovals through to full WFDF pitches. Length is line to line (the
        // playing proper, 64 m on a WFDF field), from where he lines up, so it
        // reads a little short; width is deliberately a little narrow (it
        // keeps sideline standing out).
        Assert.InRange(2 * r.Fit.HalfWidthM, 15.0, 55.0);
        Assert.InRange(2 * r.Fit.HalfLengthM, 40.0, 80.0);
    }

    [Theory]
    [MemberData(nameof(Tournaments))]
    public void TournamentAggregateWithinBand(string tag)
    {
        var all = TestFixtures.LoadBaselines();
        var b = all.Tournaments[tag];
        var games = all.Games.Where(g => g.Tournament == tag).Select(g => g.Game);

        int on = 0, off = 0, pts = 0, rests = 0, live = 0;
        double liveDist = 0;
        foreach (var g in games)
        {
            var r = FieldGeometry.Analyse(TestFixtures.LoadTrack(g).Samples);
            on += r.OnFieldSeconds; off += r.OffFieldSeconds; pts += r.PointsPlayed; rests += r.RestPeriods;
            live += r.LivePlaySeconds; liveDist += r.LivePlayDistanceM;
        }

        double frac = (double)on / (on + off);
        Assert.InRange(frac, b.OnFieldFraction - 0.02, b.OnFieldFraction + 0.02);
        Assert.InRange(pts, b.PointsPlayed - 2, b.PointsPlayed + 2);
        Assert.InRange(rests, b.RestPeriods - 2, b.RestPeriods + 2);
        Assert.InRange(on, (int)(b.OnFieldSeconds * 0.97), (int)(b.OnFieldSeconds * 1.03));
        Assert.InRange(live, (int)(b.LivePlaySeconds * 0.95), (int)(b.LivePlaySeconds * 1.05));
        Assert.InRange(liveDist, b.LivePlayDistanceM * 0.95, b.LivePlayDistanceM * 1.05);
        // Live play is most of on-field time once rests stop reading on-field
        // (0.54-0.69 across the three tournaments).
        Assert.InRange((double)live / on, 0.45, 0.75);
    }

    [Fact]
    public void SegmentsCoverTheWholeTrackAndAlternate()
    {
        var (_, samples) = TestFixtures.LoadTrack(3);
        var r = FieldGeometry.Analyse(samples);

        Assert.Equal(samples[0].T, r.Segments[0].StartT);
        Assert.Equal(samples[^1].T, r.Segments[^1].EndT);
        for (int i = 1; i < r.Segments.Count; i++)
        {
            Assert.NotEqual(r.Segments[i - 1].OnField, r.Segments[i].OnField);
            Assert.True(r.Segments[i].StartT >= r.Segments[i - 1].EndT);
        }
    }

    [Fact]
    public void OnFieldFraction_InsideALongOnFieldSegment_ReadsHigh()
    {
        var (_, samples) = TestFixtures.LoadTrack(3);
        var r = FieldGeometry.Analyse(samples);

        var seg = r.Segments.Where(s => s.OnField).OrderByDescending(s => s.EndT - s.StartT).First();
        double mid = (seg.StartT + seg.EndT) / 2;
        double f = FieldGeometry.OnFieldFraction(r, samples, mid - 20, mid + 20);
        Assert.True(f > 0.8, $"expected mostly on-field, got {f:F2}");
    }

    [Fact]
    public void OnFieldFraction_TooFewSamples_ReturnsNegative()
    {
        var (_, samples) = TestFixtures.LoadTrack(1);
        var r = FieldGeometry.Analyse(samples);
        Assert.Equal(-1, FieldGeometry.OnFieldFraction(r, samples, 10.0, 12.0));
    }
}
