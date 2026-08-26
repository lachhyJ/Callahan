using Callahan.Api.Models;
using Callahan.Api.Services;

namespace Callahan.Api.Tests;

// The classifier is geometry-based now; its real coverage is FieldGeometryTests
// against the six real games. This file covers the lap <-> geometry seam: the
// Garmin short-circuit, the no-track and no-laps branches, and the three-way
// per-lap label (incl. the Mixed case a merged lap must produce).
public class LapFieldClassifierTests
{
    private static ActivityLap Lap(int index, DateTime startGmt, double durationSec,
        double distanceM = 0, string? intensity = "INTERVAL")
        => new()
        {
            LapIndex = index,
            IntensityType = intensity,
            StartTimeGmt = startGmt,
            DurationSeconds = (decimal)durationSec,
            DistanceM = (decimal)distanceM,
        };

    private static void AssertSecondsAccounted(LapFieldSummary r, IEnumerable<ActivityLap> laps)
    {
        int nonUnknown = laps
            .Where(l => r.StateByLapIndex.TryGetValue(l.LapIndex, out var s) && s != LapFieldState.Unknown)
            .Sum(l => (int)Math.Round((double)(l.DurationSeconds ?? 0m)));
        Assert.Equal(nonUnknown, r.OnFieldSeconds + r.OffFieldSeconds + r.MixedSeconds);
    }

    // Build one lap per geometry segment of a real game, so each lap's window
    // is a single pure on/off state - what "he lapped every transition cleanly"
    // looks like.
    private static (List<ActivityLap> Laps, GeometryResult Geo, List<TrackSample> Samples, long Epoch)
        LapsFromSegments(int game)
    {
        var (epoch, samples) = TestFixtures.LoadTrack(game);
        var geo = FieldGeometry.Analyse(samples);
        var start = DateTimeOffset.FromUnixTimeMilliseconds(epoch).UtcDateTime;
        var laps = geo.Segments.Select((s, i) => Lap(
            i + 1, start.AddSeconds(s.StartT), Math.Max(1, s.EndT - s.StartT))).ToList();
        return (laps, geo, samples, epoch);
    }

    [Fact]
    public void StructuredRun_TrustsGarminLabels()
    {
        var now = new DateTime(2026, 4, 1, 9, 0, 0, DateTimeKind.Utc);
        var laps = new List<ActivityLap>
        {
            Lap(1, now, 60, intensity: "WARMUP"),
            Lap(2, now.AddSeconds(60), 15, intensity: "ACTIVE"),
            Lap(3, now.AddSeconds(75), 15, intensity: "RECOVERY"),
            Lap(4, now.AddSeconds(90), 15, intensity: "ACTIVE"),
            Lap(5, now.AddSeconds(105), 15, intensity: "COOLDOWN"),
        };

        var r = LapFieldClassifier.Classify(laps, geometry: null, samples: null, trackStartEpochMs: 0);

        Assert.Equal(LapClassifierMethod.LabelledFromGarmin, r.Method);
        Assert.Equal(LapFieldState.OnField, r.StateByLapIndex[1]);   // WARMUP
        Assert.Equal(LapFieldState.OnField, r.StateByLapIndex[2]);   // ACTIVE
        Assert.Equal(LapFieldState.OffField, r.StateByLapIndex[3]);  // RECOVERY
        Assert.Equal(LapFieldState.OffField, r.StateByLapIndex[5]);  // COOLDOWN
        Assert.Null(r.ThresholdMps);
        AssertSecondsAccounted(r, laps);
    }

    [Fact]
    public void NoTrack_LeavesEverythingUnknown()
    {
        var now = new DateTime(2026, 4, 1, 9, 0, 0, DateTimeKind.Utc);
        var laps = Enumerable.Range(1, 10).Select(i => Lap(i, now.AddSeconds(i * 60), 60)).ToList();

        var r = LapFieldClassifier.Classify(laps, geometry: null, samples: null, trackStartEpochMs: 0);

        Assert.Equal(LapClassifierMethod.NoTrack, r.Method);
        Assert.Equal(0, r.OnFieldSeconds);
        Assert.Equal(0, r.OffFieldSeconds);
        Assert.Equal(0, r.PointsPlayed);
        Assert.Equal(10, r.UnknownLapCount);
        Assert.Empty(r.StateByLapIndex);
    }

    [Fact]
    public void GeometryNoLaps_AggregatesComeFromTheSegments()
    {
        var (_, samples) = TestFixtures.LoadTrack(3);
        var geo = FieldGeometry.Analyse(samples);

        var r = LapFieldClassifier.Classify(
            new List<ActivityLap>(), geo, samples, TestFixtures.LoadTrack(3).StartEpochMs);

        Assert.Equal(LapClassifierMethod.GeometryNoLaps, r.Method);
        Assert.Equal(geo.OnFieldSeconds, r.OnFieldSeconds);
        Assert.Equal(geo.OffFieldSeconds, r.OffFieldSeconds);
        Assert.Equal(geo.PointsPlayed, r.PointsPlayed);
        Assert.True(r.OnFieldDistanceM > 0);
        Assert.Empty(r.StateByLapIndex);
    }

    [Fact]
    public void SingleWholeGameLap_FallsBackToGeometryNoLaps()
    {
        // Garmin returns one lap for the whole session when it was never
        // lap-pressed. That lap's window is the entire track, so its on-field
        // fraction is ~0.5 -> it would become one Mixed lap and collapse
        // OnFieldSeconds to 0. Must fall back to the segments instead.
        var (epoch, samples) = TestFixtures.LoadTrack(1);
        var geo = FieldGeometry.Analyse(samples);
        var start = DateTimeOffset.FromUnixTimeMilliseconds(epoch).UtcDateTime;
        var oneLap = new List<ActivityLap> { Lap(1, start, samples[^1].T) };

        var r = LapFieldClassifier.Classify(oneLap, geo, samples, epoch);

        Assert.Equal(LapClassifierMethod.GeometryNoLaps, r.Method);
        Assert.Equal(geo.OnFieldSeconds, r.OnFieldSeconds);
        Assert.Equal(geo.PointsPlayed, r.PointsPlayed);
        Assert.True(r.OnFieldSeconds > 0);
    }

    [Fact]
    public void GeometryFromLaps_CleanSegmentsLabelPurely_NoViolations()
    {
        var (laps, geo, samples, epoch) = LapsFromSegments(3);

        var r = LapFieldClassifier.Classify(laps, geo, samples, epoch);

        Assert.Equal(LapClassifierMethod.GeometryFromLaps, r.Method);
        Assert.Equal(0, r.AlternationViolations);
        // labels alternate on/off exactly like the segments they were cut from
        for (int i = 0; i < laps.Count; i++)
        {
            var expected = geo.Segments[i].OnField ? LapFieldState.OnField : LapFieldState.OffField;
            var got = r.StateByLapIndex[laps[i].LapIndex];
            if (got != LapFieldState.Unknown)   // very short segments have too few samples
                Assert.Equal(expected, got);
        }
        // points come from the geometry, not the lap count
        Assert.Equal(geo.PointsPlayed, r.PointsPlayed);
        AssertSecondsAccounted(r, laps);
    }

    [Fact]
    public void GeometryFromLaps_ALapStraddlingATransition_IsMixed()
    {
        var (_, geo, samples, epoch) = LapsFromSegments(3);
        var start = DateTimeOffset.FromUnixTimeMilliseconds(epoch).UtcDateTime;

        // Find an on->off (or off->on) segment boundary well inside the game and
        // build a single lap centred on it, long enough to be ~half each side.
        var boundary = geo.Segments
            .Zip(geo.Segments.Skip(1), (a, b) => (a, b))
            .First(p => p.a.EndT - p.a.StartT > 120 && p.b.EndT - p.b.StartT > 120);
        double mid = boundary.a.EndT;
        var straddle = Lap(1, start.AddSeconds(mid - 90), 180);

        // plus a clean lap each side so there's something to accumulate
        var before = Lap(2, start.AddSeconds(boundary.a.StartT + 5), 60);
        var after = Lap(3, start.AddSeconds(boundary.b.StartT + 5), 60);

        var r = LapFieldClassifier.Classify(
            new List<ActivityLap> { straddle, before, after }, geo, samples, epoch);

        Assert.Equal(LapClassifierMethod.GeometryFromLaps, r.Method);
        Assert.Equal(LapFieldState.Mixed, r.StateByLapIndex[1]);
        Assert.True(r.MixedSeconds >= 170, $"expected the straddling lap's ~180s in MixedSeconds, got {r.MixedSeconds}");
    }

    [Fact]
    public void GeometryFromLaps_LapWindowWithTooFewSamples_IsUnknown()
    {
        var (_, geo, samples, epoch) = LapsFromSegments(3);
        var start = DateTimeOffset.FromUnixTimeMilliseconds(epoch).UtcDateTime;

        // a 2-second window can't hold MinSamplesPerLap samples at ~2s spacing
        var tiny = Lap(1, start.AddSeconds(1000), 2);
        var normal = Lap(2, start.AddSeconds(1100), 120);

        var r = LapFieldClassifier.Classify(
            new List<ActivityLap> { tiny, normal }, geo, samples, epoch);

        Assert.Equal(LapFieldState.Unknown, r.StateByLapIndex[1]);
        Assert.Equal(1, r.UnknownLapCount);
        AssertSecondsAccounted(r, new[] { tiny, normal });
    }
}
