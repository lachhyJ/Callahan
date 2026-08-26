using Callahan.Api.Models;
using Callahan.Api.Services;

namespace Callahan.Api.Tests;

// These fixtures ARE the specification of what a lapped Ultimate game looks
// like - there's no real lapped-game data to calibrate against yet, so the
// cases below encode the intended behaviour. Speeds are m/s, durations seconds.
public class LapFieldClassifierTests
{
    private static ActivityLap Lap(
        int index, double avgSpeed, double duration,
        double? maxSpeed = null, double? distance = null, string? intensity = "INTERVAL")
        => new()
        {
            LapIndex = index,
            IntensityType = intensity,
            AvgSpeedMps = (decimal)avgSpeed,
            DurationSeconds = (decimal)duration,
            MaxSpeedMps = (decimal)(maxSpeed ?? avgSpeed * 1.5),
            DistanceM = (decimal)(distance ?? avgSpeed * duration),
        };

    // On + Off + Mixed seconds must account for every non-Unknown lap's
    // (rounded) duration exactly.
    private static void AssertSecondsAccounted(LapFieldSummary r, List<ActivityLap> laps)
    {
        int nonUnknown = laps
            .Where(l => r.StateByLapIndex[l.LapIndex] != LapFieldState.Unknown)
            .Sum(l => (int)Math.Round((double)(l.DurationSeconds ?? 0m)));
        Assert.Equal(nonUnknown, r.OnFieldSeconds + r.OffFieldSeconds + r.MixedSeconds);
    }

    [Fact]
    public void StructuredRun_LabelledFromGarmin()
    {
        var laps = new List<ActivityLap>
        {
            Lap(1, 1.5, 60, intensity: "WARMUP"),
            Lap(2, 4.7, 15, intensity: "ACTIVE"),
            Lap(3, 0.5, 15, intensity: "RECOVERY"),
            Lap(4, 4.8, 15, intensity: "ACTIVE"),
            Lap(5, 0.6, 15, intensity: "RECOVERY"),
            Lap(6, 4.9, 15, intensity: "ACTIVE"),
            Lap(7, 1.2, 120, intensity: "COOLDOWN"),
        };

        var r = LapFieldClassifier.Classify(laps);

        Assert.Equal(LapClassifierMethod.LabelledFromGarmin, r.Method);
        Assert.Equal(LapFieldState.OnField, r.StateByLapIndex[1]);  // WARMUP
        Assert.Equal(LapFieldState.OnField, r.StateByLapIndex[2]);  // ACTIVE
        Assert.Equal(LapFieldState.OffField, r.StateByLapIndex[3]); // RECOVERY
        Assert.Equal(LapFieldState.OffField, r.StateByLapIndex[7]); // COOLDOWN
        Assert.Null(r.ThresholdMps);
        AssertSecondsAccounted(r, laps);
    }

    [Fact]
    public void CleanAlternatingGame_AlternatingClean_TwelvePoints()
    {
        var laps = new List<ActivityLap>();
        for (int i = 1; i <= 24; i++)
            laps.Add(i % 2 == 1 ? Lap(i, 2.3, 45) : Lap(i, 0.5, 30));

        var r = LapFieldClassifier.Classify(laps);

        Assert.Equal(LapClassifierMethod.AlternatingClean, r.Method);
        Assert.Equal(0, r.AlternationViolations);
        Assert.Equal(12, r.PointsPlayed);
        Assert.Equal(12 * 45, r.OnFieldSeconds);
        Assert.Equal(12 * 30, r.OffFieldSeconds);
        Assert.Equal(0, r.MixedSeconds);
        Assert.Equal(0, r.UnknownLapCount);
        Assert.Equal(LapFieldState.OnField, r.StateByLapIndex[1]);
        Assert.Equal(LapFieldState.OffField, r.StateByLapIndex[2]);
        Assert.NotNull(r.ThresholdMps);
        Assert.InRange(r.ThresholdMps!.Value, 0.5m, 2.3m);
        AssertSecondsAccounted(r, laps);
    }

    [Fact]
    public void GameStartingOnSideline_StillLabelledCorrectly()
    {
        // First lap is a sideline stint - phase falls out of the speed
        // labelling, no odd/even assumption.
        var laps = new List<ActivityLap>();
        for (int i = 1; i <= 24; i++)
            laps.Add(i % 2 == 1 ? Lap(i, 0.5, 30) : Lap(i, 2.3, 45));

        var r = LapFieldClassifier.Classify(laps);

        Assert.Equal(LapClassifierMethod.AlternatingClean, r.Method);
        Assert.Equal(LapFieldState.OffField, r.StateByLapIndex[1]);
        Assert.Equal(LapFieldState.OnField, r.StateByLapIndex[2]);
        Assert.Equal(12, r.PointsPlayed);
        AssertSecondsAccounted(r, laps);
    }

    [Fact]
    public void OneMissedPress_AlternatingBroken_MergedLapFlaggedMixed()
    {
        var laps = new List<ActivityLap>();
        // Laps 1-6 alternate normally.
        for (int i = 1; i <= 6; i++)
            laps.Add(i % 2 == 1 ? Lap(i, 2.3, 45) : Lap(i, 0.5, 30));
        // Lap 7 = a point (2.3 for 45s, 103.5m) welded to a sideline stint
        // (0.5 for 30s, 15m): 118.5m / 75s. Sprinted during the point.
        laps.Add(Lap(7, avgSpeed: 118.5 / 75, duration: 75, maxSpeed: 3.45, distance: 118.5));
        // Laps 8-21 alternate, starting on-field (parity now inverted vs 1-6).
        for (int i = 8; i <= 21; i++)
            laps.Add(i % 2 == 0 ? Lap(i, 2.3, 45) : Lap(i, 0.5, 30));

        var r = LapFieldClassifier.Classify(laps);

        Assert.Equal(LapClassifierMethod.AlternatingBroken, r.Method);
        Assert.Equal(1, r.AlternationViolations);
        Assert.Equal(LapFieldState.Mixed, r.StateByLapIndex[7]);
        Assert.Equal(LapFieldState.OnField, r.StateByLapIndex[8]);
        Assert.Equal(75, r.MixedSeconds);
        Assert.Equal(11, r.PointsPlayed);   // 10 on-field laps + the Mixed one
        // The merged lap's 75s are in MixedSeconds, not OnFieldSeconds.
        Assert.Equal(10 * 45, r.OnFieldSeconds);
        AssertSecondsAccounted(r, laps);
    }

    [Fact]
    public void EveryPointGame_NoSeparation_AllOnField()
    {
        double[] speeds = { 2.0, 2.3, 2.1, 2.4, 1.9, 2.2, 2.0, 2.3, 2.1, 2.4, 1.9, 2.2, 2.0, 2.3, 2.1, 2.4, 1.9, 2.2 };
        var laps = speeds.Select((s, i) => Lap(i + 1, s, 45)).ToList();

        var r = LapFieldClassifier.Classify(laps);

        Assert.Equal(LapClassifierMethod.NoSeparation, r.Method);
        Assert.All(r.StateByLapIndex.Values, v => Assert.Equal(LapFieldState.OnField, v));
        Assert.Equal(18, r.PointsPlayed);
        Assert.Equal(0, r.OffFieldSeconds);
        Assert.Null(r.ThresholdMps);
        AssertSecondsAccounted(r, laps);
    }

    [Fact]
    public void DrillSession_RejectedByAbsoluteAnchor_NotJustRatio()
    {
        // Footwork drills ~1.8 m/s and sprint drills ~4.6 m/s: ratio ~2.6
        // clears MinCentroidRatio, so only the absolute sideline-speed anchor
        // (1.8 is not a sideline) stops this being wrongly split. Games-only
        // gating means this shouldn't reach the classifier in production; this
        // is the guard's regression test.
        var laps = new List<ActivityLap>();
        for (int i = 1; i <= 10; i++) laps.Add(Lap(i, 1.8, 40));
        for (int i = 11; i <= 20; i++) laps.Add(Lap(i, 4.6, 20));

        var r = LapFieldClassifier.Classify(laps);

        Assert.Equal(LapClassifierMethod.NoSeparation, r.Method);
        Assert.All(r.StateByLapIndex.Values, v => Assert.Equal(LapFieldState.OnField, v));
    }

    [Fact]
    public void UnbalancedGame_SplitStillFindsTheRareSidelineLaps()
    {
        // 20 on-field laps, only 2 sideline (at indices 8 and 16). Not
        // alternation, so AlternatingBroken - but the split must still isolate
        // the 2 slow laps rather than collapsing.
        var laps = new List<ActivityLap>();
        for (int i = 1; i <= 22; i++)
            laps.Add(i is 8 or 16 ? Lap(i, 0.5, 30) : Lap(i, 2.3, 45));

        var r = LapFieldClassifier.Classify(laps);

        Assert.Equal(LapClassifierMethod.AlternatingBroken, r.Method);
        Assert.Equal(LapFieldState.OffField, r.StateByLapIndex[8]);
        Assert.Equal(LapFieldState.OffField, r.StateByLapIndex[16]);
        Assert.Equal(20, r.PointsPlayed);
        Assert.Equal(2 * 30, r.OffFieldSeconds);
        Assert.Equal(0, r.MixedSeconds);   // the on-field runs are all normal length
        AssertSecondsAccounted(r, laps);
    }

    [Fact]
    public void ThreeLaps_TooFewToSplit()
    {
        var laps = new List<ActivityLap> { Lap(1, 2.3, 45), Lap(2, 0.5, 30), Lap(3, 2.3, 45) };

        var r = LapFieldClassifier.Classify(laps);

        Assert.Equal(LapClassifierMethod.TooFewLaps, r.Method);
        Assert.All(r.StateByLapIndex.Values, v => Assert.Equal(LapFieldState.OnField, v));
        Assert.Equal(3, r.PointsPlayed);
        AssertSecondsAccounted(r, laps);
    }

    [Fact]
    public void IdenticalSpeeds_NoSeparation()
    {
        var laps = Enumerable.Range(1, 8).Select(i => Lap(i, 2.0, 45)).ToList();

        var r = LapFieldClassifier.Classify(laps);

        Assert.Equal(LapClassifierMethod.NoSeparation, r.Method);
        Assert.All(r.StateByLapIndex.Values, v => Assert.Equal(LapFieldState.OnField, v));
    }

    [Fact]
    public void NullAvgSpeed_DerivedFromDistanceAndDuration()
    {
        var laps = new List<ActivityLap>();
        for (int i = 1; i <= 8; i++)
            laps.Add(i % 2 == 1 ? Lap(i, 2.3, 45) : Lap(i, 0.5, 30));
        // Lap 3: no avg speed, but distance/duration => 2.3 m/s.
        var l3 = laps[2];
        l3.AvgSpeedMps = null;
        l3.DistanceM = (decimal)(2.3 * 45);
        l3.DurationSeconds = 45;
        l3.MaxSpeedMps = 3.45m;

        var r = LapFieldClassifier.Classify(laps);

        Assert.Equal(0, r.UnknownLapCount);
        Assert.Equal(LapFieldState.OnField, r.StateByLapIndex[3]);
        Assert.Equal(LapClassifierMethod.AlternatingClean, r.Method);
        AssertSecondsAccounted(r, laps);
    }

    [Fact]
    public void FullyNullLap_IsUnknownAndExcludedFromAllBuckets()
    {
        var laps = new List<ActivityLap>();
        for (int i = 1; i <= 8; i++)
            laps.Add(i % 2 == 1 ? Lap(i, 2.3, 45) : Lap(i, 0.5, 30));
        laps.Add(new ActivityLap
        {
            LapIndex = 9,
            IntensityType = "INTERVAL",
            AvgSpeedMps = null,
            DistanceM = null,
            DurationSeconds = 20,
            MaxSpeedMps = null,
        });

        var r = LapFieldClassifier.Classify(laps);

        Assert.Equal(1, r.UnknownLapCount);
        Assert.Equal(LapFieldState.Unknown, r.StateByLapIndex[9]);
        // The 20 unknown seconds are excluded: only laps 1-8 accounted.
        Assert.Equal(4 * 45 + 4 * 30, r.OnFieldSeconds + r.OffFieldSeconds + r.MixedSeconds);
        AssertSecondsAccounted(r, laps);
    }

    [Fact]
    public void GpsSpikeLap_DoesNotDistortTheSplit()
    {
        // One on-field lap reads an absurd 12 m/s (GPS glitch). Log-space
        // 2-means must keep it in the fast cluster, not carve a 1-vs-rest split.
        var laps = new List<ActivityLap>();
        for (int i = 1; i <= 12; i++)
            laps.Add(i % 2 == 1 ? Lap(i, 2.3, 45) : Lap(i, 0.5, 30));
        laps[0] = Lap(1, 12.0, 45);   // the spike, at an on-field position

        var r = LapFieldClassifier.Classify(laps);

        Assert.Equal(LapClassifierMethod.AlternatingClean, r.Method);
        Assert.Equal(LapFieldState.OnField, r.StateByLapIndex[1]);
        Assert.Equal(LapFieldState.OnField, r.StateByLapIndex[3]);
        Assert.Equal(LapFieldState.OffField, r.StateByLapIndex[2]);
        Assert.Equal(6, r.PointsPlayed);
        AssertSecondsAccounted(r, laps);
    }

    [Fact]
    public void SprintVetoCanLeaveOnlyOneState_AdaptiveSplit()
    {
        // Sideline laps where he sprinted to the line each time (max 4.5) get
        // force-flipped on-field by the sprint veto, leaving no off-field laps.
        var laps = new List<ActivityLap>();
        for (int i = 1; i <= 8; i++)
            laps.Add(i % 2 == 1
                ? Lap(i, 2.3, 45)
                : Lap(i, 0.8, 30, maxSpeed: 4.5));

        var r = LapFieldClassifier.Classify(laps);

        Assert.Equal(LapClassifierMethod.AdaptiveSplit, r.Method);
        Assert.All(r.StateByLapIndex.Values, v => Assert.Equal(LapFieldState.OnField, v));
        Assert.Equal(8, r.PointsPlayed);
        AssertSecondsAccounted(r, laps);
    }

    [Fact]
    public void StraySidelineSprint_VetoFlipsThatLapOnField_DocumentedTradeoff()
    {
        var laps = new List<ActivityLap>();
        for (int i = 1; i <= 8; i++)
            laps.Add(i % 2 == 1 ? Lap(i, 2.3, 45) : Lap(i, 0.5, 30));
        // Lap 4 is a sideline stint, but he jogged hard after a rolling disc.
        laps[3] = Lap(4, 0.5, 30, maxSpeed: 5.0);

        var r = LapFieldClassifier.Classify(laps);

        // The veto wins: this sideline lap is counted as a point. Deliberate.
        Assert.Equal(LapFieldState.OnField, r.StateByLapIndex[4]);
        Assert.Equal(LapClassifierMethod.AlternatingBroken, r.Method);
        Assert.Equal(2, r.AlternationViolations);
        Assert.Equal(0, r.MixedSeconds);
        AssertSecondsAccounted(r, laps);
    }

    [Fact]
    public void BackToBackPointsBothLapped_BrokenButNothingMerged()
    {
        // Played two points without subbing, lapped each. Legit, not a missed
        // press: AlternatingBroken (it isn't alternation) but no Mixed, because
        // neither back-to-back lap is anomalously long.
        var pattern = new[] { true, false, true, true, false, true, false, true, false, true };
        var laps = pattern.Select((on, i) => on ? Lap(i + 1, 2.3, 45) : Lap(i + 1, 0.5, 30)).ToList();

        var r = LapFieldClassifier.Classify(laps);

        Assert.Equal(LapClassifierMethod.AlternatingBroken, r.Method);
        Assert.Equal(1, r.AlternationViolations);
        Assert.Equal(0, r.MixedSeconds);
        Assert.DoesNotContain(LapFieldState.Mixed, r.StateByLapIndex.Values);
        Assert.Equal(6, r.PointsPlayed);
        AssertSecondsAccounted(r, laps);
    }
}
