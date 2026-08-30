using Callahan.Api.DTOs;
using Callahan.Api.Services;

namespace Callahan.Api.Tests;

public class MonthlyWellnessSummarizerTests
{
    private static DailyWellnessDto Row(
        DateOnly date, int? readiness = null, int? sleepScore = null, int? sleepSeconds = null,
        int? hrv = null, int? rhr = null) =>
        new(0, date, sleepSeconds, null, null, null, null, sleepScore, null, hrv, null, null, readiness, null, null, rhr, null, null, null);

    private static List<DailyWellnessDto> Days(
        DateOnly start, int days, int? readiness = null, int? sleepScore = null,
        int? sleepSeconds = null, int? hrv = null, int? rhr = null) =>
        Enumerable.Range(0, days).Select(i => Row(start.AddDays(i), readiness, sleepScore, sleepSeconds, hrv, rhr)).ToList();

    private static readonly DateOnly MonthStart = new(2026, 8, 1);
    private static readonly DateOnly TrailStart = new(2026, 5, 1);

    private static WellnessMetricDto Metric(WellnessSectionDto s, string key) => s.Metrics.Single(m => m.Key == key);

    [Fact]
    public void ReturnsNull_WhenMonthHasFewerThanSevenLoggedDaysForEveryMetric()
    {
        var month = Days(MonthStart, 6, readiness: 70, sleepScore: 80, sleepSeconds: 27000, hrv: 50, rhr: 44);
        var trailing = Days(TrailStart, 60, readiness: 70, sleepScore: 80, sleepSeconds: 27000, hrv: 50, rhr: 44);

        var result = MonthlyWellnessSummarizer.Summarize(month, trailing, 31, [], 70);

        Assert.Null(result);
    }

    [Fact]
    public void FlatMonthVsFlatBaseline_IsInLine()
    {
        var month = Days(MonthStart, 28, readiness: 68, sleepScore: 80, sleepSeconds: 27000, hrv: 50, rhr: 44);
        var trailing = Days(TrailStart, 80, readiness: 68, sleepScore: 80, sleepSeconds: 27000, hrv: 50, rhr: 44);

        var result = MonthlyWellnessSummarizer.Summarize(month, trailing, 31, [], 68)!;

        Assert.Equal("in_line", Metric(result, "readiness").Direction);
        Assert.Equal(68m, Metric(result, "readiness").MonthAvg);
        Assert.Equal(68m, Metric(result, "readiness").TrailingAvg);
    }

    [Fact]
    public void ReadinessWellBelowBaseline_IsFlaggedBelow()
    {
        var month = Days(MonthStart, 28, readiness: 45);
        var trailing = Days(TrailStart, 80, readiness: 70);

        var result = MonthlyWellnessSummarizer.Summarize(month, trailing, 31, [], 70)!;

        Assert.Equal("below", Metric(result, "readiness").Direction);
    }

    [Fact]
    public void ElevatedRestingHr_ReadsAsWorseRecovery_Below()
    {
        // Resting HR up ~10% from baseline — lower-is-better, so this is the
        // fatigue direction and must map to "below".
        var month = Days(MonthStart, 28, rhr: 48);
        var trailing = Days(TrailStart, 80, rhr: 44);

        var result = MonthlyWellnessSummarizer.Summarize(month, trailing, 31, [], null)!;

        Assert.Equal("below", Metric(result, "restingHeartRate").Direction);
    }

    [Fact]
    public void Coverage_CountsLoggedNightsAndNightsUnderSevenHours()
    {
        var month = new List<DailyWellnessDto>();
        month.AddRange(Days(MonthStart, 10, sleepSeconds: 21600));       // 6h — under 7h
        month.AddRange(Days(MonthStart.AddDays(10), 12, sleepSeconds: 28800)); // 8h
        // 8 further days with no sleep reading at all
        month.AddRange(Days(MonthStart.AddDays(22), 8, readiness: 60));

        var trailing = Days(TrailStart, 80, sleepSeconds: 27000);

        var result = MonthlyWellnessSummarizer.Summarize(month, trailing, 31, [], null)!;

        Assert.Equal(22, result.NightsLogged);
        Assert.Equal(31, result.DaysInMonth);
        Assert.Equal(10, result.NightsUnder7h);
    }

    [Fact]
    public void LoadVsRecoveryLine_EmittedWhenTopVolumeWeeksReadinessDipsBelowBaseline()
    {
        var month = Days(MonthStart, 28, readiness: 65);
        var trailing = Days(TrailStart, 80, readiness: 65);
        var weeks = new List<WellnessWeekLoad>
        {
            new(12000m, 55.0),   // highest volume, low readiness
            new(11000m, 57.0),   // 2nd highest, low readiness
            new(4000m, 70.0),
            new(3000m, 72.0),
        };

        var result = MonthlyWellnessSummarizer.Summarize(month, trailing, 31, weeks, trailingReadinessAvg: 70)!;

        Assert.NotNull(result.LoadVsRecoveryLine);
        Assert.Contains("highest-volume weeks", result.LoadVsRecoveryLine);
    }

    [Fact]
    public void LoadVsRecoveryLine_NullWhenGapWithinBand()
    {
        var month = Days(MonthStart, 28, readiness: 68);
        var trailing = Days(TrailStart, 80, readiness: 68);
        var weeks = new List<WellnessWeekLoad>
        {
            new(12000m, 69.0),
            new(11000m, 68.0),
            new(4000m, 70.0),
        };

        var result = MonthlyWellnessSummarizer.Summarize(month, trailing, 31, weeks, trailingReadinessAvg: 70)!;

        Assert.Null(result.LoadVsRecoveryLine);
    }

    [Fact]
    public void LoadVsRecoveryLine_NullWhenTooFewUsableWeeks()
    {
        var month = Days(MonthStart, 28, readiness: 65);
        var trailing = Days(TrailStart, 80, readiness: 65);
        var weeks = new List<WellnessWeekLoad> { new(12000m, 50.0), new(11000m, 52.0) };

        var result = MonthlyWellnessSummarizer.Summarize(month, trailing, 31, weeks, trailingReadinessAvg: 70)!;

        Assert.Null(result.LoadVsRecoveryLine);
    }

    [Fact]
    public void TrailingWindowUnderSevenDays_LeavesDirectionInsufficient()
    {
        var month = Days(MonthStart, 28, readiness: 60);
        var trailing = Days(TrailStart, 4, readiness: 70);

        var result = MonthlyWellnessSummarizer.Summarize(month, trailing, 31, [], 70)!;

        var readiness = Metric(result, "readiness");
        Assert.Equal(60m, readiness.MonthAvg);
        Assert.Null(readiness.TrailingAvg);
        Assert.Equal("insufficient", readiness.Direction);
    }
}
