using Callahan.Api.Services;

namespace Callahan.Api.Tests;

// Oracle: scripts/ultimate-stream-explore/segment.py on every tournament game
// synced so far - 6 Regionals + 5 Big C + 6 Div 2 Nationals. Big C was played
// on a constrained club oval (shorter fields), so this set spans field regimes
// deliberately: a geometry change that only holds on WFDF-standard sizes fails
// here. Bands, not equality: Math.Atan2 / double ordering / index percentiles
// won't reproduce CPython bit-for-bit, and an exact suite would be flaky.
public class FieldGeometryTests
{
    public static IEnumerable<object[]> Games =>
        Enumerable.Range(1, 17).Select(g => new object[] { g });

    public static IEnumerable<object[]> Tournaments =>
        new[] { "Regionals", "BigC", "Nationals" }.Select(t => new object[] { t });

    [Theory]
    [MemberData(nameof(Games))]
    public void MatchesSegmentPyBaseline(int game)
    {
        var baseline = TestFixtures.LoadBaselines().Games.Single(b => b.Game == game);
        var (_, samples) = TestFixtures.LoadTrack(game);

        var r = FieldGeometry.Analyse(samples);

        double frac = (double)r.OnFieldSeconds / (r.OnFieldSeconds + r.OffFieldSeconds);
        Assert.InRange(frac, baseline.OnFieldFraction - 0.03, baseline.OnFieldFraction + 0.03);
        Assert.InRange(r.PointsPlayed, baseline.PointsPlayed - 1, baseline.PointsPlayed + 1);

        // Live play (on-field time inside a point) tracks segment.py within a
        // band, and can never exceed total on-field time.
        Assert.InRange(r.LivePlaySeconds, baseline.LivePlaySeconds * 0.90, baseline.LivePlaySeconds * 1.10);
        Assert.True(r.LivePlaySeconds <= r.OnFieldSeconds);

        // Live-play distance is summed over the same windows: bands against the
        // fixture, and never more than total on-field distance.
        Assert.InRange(r.LivePlayDistanceM, baseline.LivePlayDistanceM * 0.90, baseline.LivePlayDistanceM * 1.10);
        Assert.True(r.LivePlayDistanceM <= r.OnFieldDistanceM + 1.0);

        int dur = r.OnFieldSeconds + r.OffFieldSeconds;
        Assert.InRange(dur, (int)(baseline.DurationSeconds * 0.98), (int)(baseline.DurationSeconds * 1.02));

        Assert.InRange(2 * r.Fit.HalfWidthM, baseline.FieldWidthM * 0.90, baseline.FieldWidthM * 1.10);
        Assert.InRange(2 * r.Fit.HalfLengthM, baseline.FieldLengthM * 0.90, baseline.FieldLengthM * 1.10);

        // Still recognisably a field. Wide band on purpose: the fitted frame
        // spans club ovals through to full WFDF pitches, and a per-game theta
        // wobble can smear length into width (see the 2026-08-30 investigation).
        Assert.InRange(2 * r.Fit.HalfWidthM, 15.0, 55.0);
        Assert.InRange(2 * r.Fit.HalfLengthM, 45.0, 110.0);
    }

    [Theory]
    [MemberData(nameof(Tournaments))]
    public void TournamentAggregateWithinBand(string tag)
    {
        var all = TestFixtures.LoadBaselines();
        var b = all.Tournaments[tag];
        var games = all.Games.Where(g => g.Tournament == tag).Select(g => g.Game);

        int on = 0, off = 0, pts = 0, live = 0;
        double liveDist = 0;
        foreach (var g in games)
        {
            var r = FieldGeometry.Analyse(TestFixtures.LoadTrack(g).Samples);
            on += r.OnFieldSeconds; off += r.OffFieldSeconds; pts += r.PointsPlayed;
            live += r.LivePlaySeconds; liveDist += r.LivePlayDistanceM;
        }

        double frac = (double)on / (on + off);
        Assert.InRange(frac, b.OnFieldFraction - 0.02, b.OnFieldFraction + 0.02);
        Assert.InRange(pts, b.PointsPlayed - 4, b.PointsPlayed + 4);
        Assert.InRange(on, (int)(b.OnFieldSeconds * 0.97), (int)(b.OnFieldSeconds * 1.03));
        Assert.InRange(live, (int)(b.LivePlaySeconds * 0.95), (int)(b.LivePlaySeconds * 1.05));
        Assert.InRange(liveDist, b.LivePlayDistanceM * 0.95, b.LivePlayDistanceM * 1.05);
        // Live play is roughly half of on-field time across every tournament.
        Assert.InRange((double)live / on, 0.40, 0.62);
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
