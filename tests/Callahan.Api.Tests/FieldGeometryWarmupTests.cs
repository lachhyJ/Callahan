using Callahan.Api.Models;
using Callahan.Api.Services;

namespace Callahan.Api.Tests;

// The per-activity "warmup recorded?" flag (Activity.WarmupRecorded) fixing the
// gameplay start instead of inferring it from line-ups. Mirrors
// scripts/ultimate-stream-explore/test_fieldgeometry_v2.py branch for branch;
// the expected numbers on the real fixtures come from fieldgeometry_v2.py.
public class FieldGeometryWarmupTests
{
    private static readonly DateTime Epoch = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly long EpochMs = new DateTimeOffset(Epoch).ToUnixTimeMilliseconds();

    private static ActivityLap Lap(int index, double startSec, double durationSec)
        => new()
        {
            LapIndex = index,
            StartTimeGmt = Epoch.AddSeconds(startSec),
            DurationSeconds = (decimal)durationSec,
            AvgSpeedMps = 1.2m,
            MaxSpeedMps = 6.0m,
        };

    private static double? Start(bool? warmupRecorded, params ActivityLap[] laps)
        => LapFieldClassifier.KnownGameplayStart(laps, EpochMs, warmupRecorded);

    [Fact]
    public void No_PinsStartToZero()
        => Assert.Equal(0.0, Start(false, Lap(1, 0, 1200), Lap(2, 1200, 120)));

    [Fact]
    public void DontKnow_Infers()
        => Assert.Null(Start(null, Lap(1, 0, 1200), Lap(2, 1200, 120)));

    [Fact]
    public void Yes_WithWarmupLap_IsItsEnd()
        => Assert.Equal(1200, Start(true, Lap(1, 0, 1200), Lap(2, 1200, 120), Lap(3, 1320, 90))!.Value, 3);

    [Fact]
    public void Yes_WithoutWarmupLap_Infers()
    {
        Assert.Null(Start(true, Lap(1, 0, 300), Lap(2, 300, 120)));
        Assert.Null(Start(true));
    }

    [Fact]
    public void WarmupLap_IsFoundByDuration_NotPosition()
    {
        // Lap 1 is sideline time before the warmup: the warmup is lap 2.
        Assert.Equal(1700, Start(true, Lap(1, 0, 200), Lap(2, 200, 1500), Lap(3, 1700, 120))!.Value, 3);
        // Index order doesn't matter either, only start time.
        Assert.Equal(1700, Start(true, Lap(3, 1700, 120), Lap(1, 0, 200), Lap(2, 200, 1500))!.Value, 3);
    }

    [Fact]
    public void EarliestWarmupLapWins()
        => Assert.Equal(1000, Start(true, Lap(1, 0, 1000), Lap(2, 1000, 120), Lap(3, 1120, 1200), Lap(4, 2320, 90))!.Value, 3);

    [Fact]
    public void SingleLongLap_IsNotAPress()
    {
        // Garmin's one default lap over the whole session ...
        Assert.Null(Start(true, Lap(1, 0, 5400)));
        // ... or a long last lap with nothing after it.
        Assert.Null(Start(true, Lap(1, 0, 300), Lap(2, 300, 5000)));
    }

    [Fact]
    public void LapWithNoStartTime_IsIgnored()
    {
        var noStart = Lap(1, 0, 1500);
        noStart.StartTimeGmt = null;
        Assert.Null(Start(true, noStart, Lap(2, 1500, 120)));
    }

    [Fact]
    public void NullStart_LeavesTheInferenceUnchanged()
    {
        var (_, samples) = TestFixtures.LoadTrack(5);
        var a = FieldGeometry.Analyse(samples);
        var b = FieldGeometry.Analyse(samples, knownStartT: null);
        Assert.Equal(a.GameplayStartT, b.GameplayStartT);
        Assert.Equal(a.OnFieldSeconds, b.OnFieldSeconds);
        Assert.Equal(a.PointsPlayed, b.PointsPlayed);
    }

    // Colossus: inferred start ~59 min, so off-field for the first hour. "No"
    // pins it to 0. Reference (fieldgeometry_v2.py, start=0): on-field 2822 s,
    // 13 points, 5 rest periods, width 31.0 m.
    [Fact]
    public void No_FixesGame15sLateStart()
    {
        var (epoch, samples) = TestFixtures.LoadTrack(15);
        var inferred = LapFieldClassifier.AnalyseWithLaps([], samples, epoch);
        var pinned = LapFieldClassifier.AnalyseWithLaps([], samples, epoch, warmupRecorded: false);

        Assert.True(inferred.GameplayStartT > 3000, $"inferred start {inferred.GameplayStartT}");
        Assert.Equal(0, pinned.GameplayStartT);
        Assert.InRange(pinned.OnFieldSeconds, 2822 * 0.97, 2822 * 1.03);
        Assert.InRange(pinned.PointsPlayed, 12, 14);
        Assert.InRange(pinned.RestPeriods, 4, 6);
        Assert.InRange(2 * pinned.Fit.HalfWidthM, 30.0, 32.0);
    }

    // Chilly #3: the inference finds no start (line-ups mostly at one end), so
    // the warmup reads as play. "Yes" plus the pressed warmup lap fixes it, and
    // the counts then match the laps exactly (15 points, 13 rest periods).
    [Fact]
    public void Yes_WithWarmupLap_FixesChilly()
    {
        var (epoch, samples, _, rows) = TestFixtures.LoadTryout(3);
        var inferred = LapFieldClassifier.AnalyseWithLaps(rows, samples, epoch);
        var start = LapFieldClassifier.KnownGameplayStart(rows, epoch, true);
        var pinned = LapFieldClassifier.AnalyseWithLaps(rows, samples, epoch, warmupRecorded: true);

        Assert.Equal(0, inferred.GameplayStartT);
        Assert.NotNull(start);
        Assert.InRange(start!.Value, 1395, 1396);
        Assert.InRange(pinned.GameplayStartT, start.Value - 2, start.Value + 2);
        Assert.Equal(15, pinned.PointsPlayed);
        Assert.Equal(13, pinned.RestPeriods);
    }

    [Fact]
    public void NothingOnFieldBeforeAKnownStart()
    {
        var (epoch, samples, _, rows) = TestFixtures.LoadTryout(1);
        var start = LapFieldClassifier.KnownGameplayStart(rows, epoch, true)!.Value;
        var r = LapFieldClassifier.AnalyseWithLaps(rows, samples, epoch, warmupRecorded: true);

        for (int i = 0; i < samples.Count; i++)
            if (samples[i].T < start) Assert.False(r.OnFieldBySample[i], $"sample {i} at {samples[i].T}s on-field");
        Assert.Contains(true, r.OnFieldBySample);
    }
}
