using Callahan.Api.DTOs;
using Callahan.Api.Services;

namespace Callahan.Api.Tests;

public class RunningMetricsTests
{
    private static RunActivityInput Run(
        string type, decimal? km = 5m, int seconds = 1800,
        decimal? highSpeedM = 1200m, int activeLaps = 16) =>
        new(type, km, seconds, highSpeedM, activeLaps);

    [Fact]
    public void EasyAerobicRun_ReportsDistanceAndDuration_NotReps()
    {
        var result = RunningMetrics.Summarize([Run("Easy Aerobic Run", km: 8m, seconds: 2700)]).Single();

        Assert.Equal(1, result.Count);
        Assert.Equal(8m, result.TotalDistanceKm);
        Assert.Equal(2700, result.TotalDurationSeconds);
        Assert.Null(result.HighSpeedDistanceKm);
        Assert.Null(result.WorkRepCount);
    }

    // GPS under-measures shuttle turns and elapsed time counts standing rest,
    // so neither total is allowed out for the rep-based types.
    [Fact]
    public void HighSpeedIntervals_ReportsWorkNotTotals()
    {
        var result = RunningMetrics.Summarize([
            Run("High Speed Intervals", highSpeedM: 1168m, activeLaps: 16),
            Run("High Speed Intervals", highSpeedM: 1000m, activeLaps: 14),
        ]).Single();

        Assert.Equal(2, result.Count);
        Assert.Null(result.TotalDistanceKm);
        Assert.Null(result.TotalDurationSeconds);
        Assert.Equal(2.17m, result.HighSpeedDistanceKm);
        Assert.Equal(30, result.WorkRepCount);
    }

    [Fact]
    public void SpeedAndAcceleration_ReportsRepsOnly()
    {
        var result = RunningMetrics.Summarize([Run("Speed & Acceleration", activeLaps: 12)]).Single();

        Assert.Null(result.TotalDistanceKm);
        Assert.Null(result.TotalDurationSeconds);
        Assert.Null(result.HighSpeedDistanceKm);
        Assert.Equal(12, result.WorkRepCount);
    }

    // No laps synced is "we don't know", not "zero reps".
    [Fact]
    public void RepBasedTypeWithNoLapData_LeavesRepsNull()
    {
        var result = RunningMetrics.Summarize([Run("Speed & Acceleration", activeLaps: 0)]).Single();

        Assert.Equal(1, result.Count);
        Assert.Null(result.WorkRepCount);
    }

    [Fact]
    public void IntervalsWithNoHighSpeedDistance_LeavesItNull()
    {
        var result = RunningMetrics.Summarize([Run("High Speed Intervals", highSpeedM: null)]).Single();

        Assert.Null(result.HighSpeedDistanceKm);
        Assert.Equal(16, result.WorkRepCount);
    }

    [Fact]
    public void UnknownType_FallsBackToDistanceAndDuration()
    {
        var result = RunningMetrics.Summarize([Run("Unspecified run", km: 4m, seconds: 1500)]).Single();

        Assert.Equal(4m, result.TotalDistanceKm);
        Assert.Equal(1500, result.TotalDurationSeconds);
        Assert.Null(result.WorkRepCount);
    }

    [Fact]
    public void TypesAreOrderedByCount()
    {
        var result = RunningMetrics.Summarize([
            Run("Easy Aerobic Run"),
            Run("High Speed Intervals"),
            Run("High Speed Intervals"),
            Run("High Speed Intervals"),
        ]);

        Assert.Equal("High Speed Intervals", result[0].TypeName);
        Assert.Equal(3, result[0].Count);
    }
}

public class PushPullBalanceTests
{
    [Fact]
    public void PullExecutedWellBelowPush_IsFlagged()
    {
        var line = PushPullBalance.Flag(new PushPullInput(
            PlannedPush: 50, PlannedPull: 52, ActualPush: 48, ActualPull: 38));

        Assert.NotNull(line);
        Assert.StartsWith("Pull sets came in at 73% of plan", line);
        Assert.Contains("38 logged of 52 prescribed", line);
    }

    [Fact]
    public void PushExecutedWellBelowPull_NamesPush()
    {
        var line = PushPullBalance.Flag(new PushPullInput(
            PlannedPush: 50, PlannedPull: 50, ActualPush: 30, ActualPull: 50));

        Assert.NotNull(line);
        Assert.StartsWith("Push sets came in at 60% of plan", line);
    }

    [Fact]
    public void BothSidesExecutedEvenly_IsSilent()
    {
        var line = PushPullBalance.Flag(new PushPullInput(
            PlannedPush: 50, PlannedPull: 52, ActualPush: 47, ActualPull: 49));

        Assert.Null(line);
    }

    // The whole point of comparing rates rather than raw counts: a light
    // month drags both sides down together and shouldn't flag.
    [Fact]
    public void LightMonthWithBothSidesDownEqually_IsSilent()
    {
        var line = PushPullBalance.Flag(new PushPullInput(
            PlannedPush: 60, PlannedPull: 60, ActualPush: 24, ActualPull: 24));

        Assert.Null(line);
    }

    // A program that's push-heavy by design isn't a finding — only a gap
    // between planned and logged is.
    [Fact]
    public void AsymmetricProgramFullyExecuted_IsSilent()
    {
        var line = PushPullBalance.Flag(new PushPullInput(
            PlannedPush: 80, PlannedPull: 30, ActualPush: 80, ActualPull: 30));

        Assert.Null(line);
    }

    [Fact]
    public void NothingPrescribedOnASide_IsSilent()
    {
        Assert.Null(PushPullBalance.Flag(new PushPullInput(0, 0, 0, 0)));
        Assert.Null(PushPullBalance.Flag(new PushPullInput(40, 0, 40, 12)));
    }
}

public class RebalanceQuestionTests
{
    private static SessionTypeCountDto T(string label, int count, string family) => new(label, count, family);

    // The bug this replaced: max/min was taken across every label, so an
    // Ultimate session type could be "rebalanced" against a run type.
    [Fact]
    public void NeverComparesAcrossFamilies()
    {
        var question = MonthlyReportBuilder.RebalanceQuestion([
            T("Club Training", 6, SessionFamily.Ultimate),
            T("Speed & Acceleration", 1, SessionFamily.Running),
        ]);

        Assert.Null(question);
    }

    [Fact]
    public void FlagsLopsidedPairWithinTheGymFamily()
    {
        var question = MonthlyReportBuilder.RebalanceQuestion([
            T("Workout 1", 6, SessionFamily.Gym),
            T("Workout 2", 2, SessionFamily.Gym),
            T("Easy Aerobic Run", 1, SessionFamily.Running),
        ]);

        Assert.NotNull(question);
        Assert.Contains("Workout 1 ran 6x", question);
        Assert.Contains("Workout 2's 2x", question);
    }

    [Fact]
    public void PrefersGymWhenMultipleFamiliesQualify()
    {
        var question = MonthlyReportBuilder.RebalanceQuestion([
            T("Workout 1", 4, SessionFamily.Gym),
            T("Workout 2", 2, SessionFamily.Gym),
            T("Club Training", 8, SessionFamily.Ultimate),
            T("Throws", 2, SessionFamily.Ultimate),
        ]);

        Assert.Contains("Workout 1", question);
    }

    [Fact]
    public void FallsThroughToANonGymFamilyWhenGymIsBalanced()
    {
        var question = MonthlyReportBuilder.RebalanceQuestion([
            T("Workout 1", 4, SessionFamily.Gym),
            T("Workout 2", 4, SessionFamily.Gym),
            T("Club Training", 8, SessionFamily.Ultimate),
            T("Throws", 2, SessionFamily.Ultimate),
        ]);

        Assert.Contains("Club Training", question);
    }

    // 2x vs 1x satisfies "twice as often" but is too thin to be worth asking
    // about — the bar is 3.
    [Fact]
    public void TwoVersusOne_IsTooThinToAsk()
    {
        Assert.Null(MonthlyReportBuilder.RebalanceQuestion([
            T("Easy Aerobic Run", 2, SessionFamily.Running),
            T("Speed & Acceleration", 1, SessionFamily.Running),
        ]));
    }

    [Fact]
    public void ThreeVersusOne_DoesAsk()
    {
        Assert.NotNull(MonthlyReportBuilder.RebalanceQuestion([
            T("Easy Aerobic Run", 3, SessionFamily.Running),
            T("Speed & Acceleration", 1, SessionFamily.Running),
        ]));
    }

    [Fact]
    public void SingleTypeInAFamily_IsNotAComparison()
    {
        Assert.Null(MonthlyReportBuilder.RebalanceQuestion([T("Workout 1", 9, SessionFamily.Gym)]));
    }
}
