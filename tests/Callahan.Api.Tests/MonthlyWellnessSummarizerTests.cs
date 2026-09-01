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
        var month = Days(MonthStart, 6, sleepScore: 80, sleepSeconds: 27000, hrv: 50, rhr: 44);
        var trailing = Days(TrailStart, 60, sleepScore: 80, sleepSeconds: 27000, hrv: 50, rhr: 44);

        var result = MonthlyWellnessSummarizer.Summarize(month, trailing, 31);

        Assert.Null(result);
    }

    // Readiness is deliberately not a monthly metric — it's acute and
    // mean-reverting, so a month average against a 3-month baseline carries
    // almost no information. It stays on the dashboard, same-day.
    [Fact]
    public void ReadinessIsNotReported_AndSlowMovingMetricsLead()
    {
        var month = Days(MonthStart, 28, readiness: 45, sleepScore: 80, sleepSeconds: 27000, hrv: 50, rhr: 44);
        var trailing = Days(TrailStart, 80, readiness: 70, sleepScore: 80, sleepSeconds: 27000, hrv: 50, rhr: 44);

        var result = MonthlyWellnessSummarizer.Summarize(month, trailing, 31)!;

        Assert.DoesNotContain(result.Metrics, m => m.Key == "readiness");
        Assert.Equal(
            new[] { "restingHeartRate", "hrv", "sleepDuration", "sleepScore" },
            result.Metrics.Select(m => m.Key));
    }

    [Fact]
    public void FlatMonthVsFlatBaseline_IsInLine()
    {
        var month = Days(MonthStart, 28, sleepScore: 80, sleepSeconds: 27000, hrv: 50, rhr: 44);
        var trailing = Days(TrailStart, 80, sleepScore: 80, sleepSeconds: 27000, hrv: 50, rhr: 44);

        var result = MonthlyWellnessSummarizer.Summarize(month, trailing, 31)!;

        Assert.Equal("in_line", Metric(result, "hrv").Direction);
        Assert.Equal(50m, Metric(result, "hrv").MonthAvg);
        Assert.Equal(50m, Metric(result, "hrv").TrailingAvg);
    }

    [Fact]
    public void HrvWellBelowBaseline_IsFlaggedBelow()
    {
        var month = Days(MonthStart, 28, hrv: 40);
        var trailing = Days(TrailStart, 80, hrv: 55);

        var result = MonthlyWellnessSummarizer.Summarize(month, trailing, 31)!;

        Assert.Equal("below", Metric(result, "hrv").Direction);
    }

    [Fact]
    public void ElevatedRestingHr_ReadsAsWorseRecovery_Below()
    {
        // Resting HR up ~10% from baseline — lower-is-better, so this is the
        // fatigue direction and must map to "below".
        var month = Days(MonthStart, 28, rhr: 48);
        var trailing = Days(TrailStart, 80, rhr: 44);

        var result = MonthlyWellnessSummarizer.Summarize(month, trailing, 31)!;

        Assert.Equal("below", Metric(result, "restingHeartRate").Direction);
    }

    [Fact]
    public void Coverage_CountsLoggedNightsAndNightsUnderSevenHours()
    {
        var month = new List<DailyWellnessDto>();
        month.AddRange(Days(MonthStart, 10, sleepSeconds: 21600));       // 6h — under 7h
        month.AddRange(Days(MonthStart.AddDays(10), 12, sleepSeconds: 28800)); // 8h
        // 8 further days with no sleep reading at all
        month.AddRange(Days(MonthStart.AddDays(22), 8, rhr: 44));

        var trailing = Days(TrailStart, 80, sleepSeconds: 27000);

        var result = MonthlyWellnessSummarizer.Summarize(month, trailing, 31)!;

        Assert.Equal(22, result.NightsLogged);
        Assert.Equal(31, result.DaysInMonth);
        Assert.Equal(10, result.NightsUnder7h);
    }

    [Fact]
    public void TrailingWindowUnderSevenDays_LeavesDirectionInsufficient()
    {
        var month = Days(MonthStart, 28, rhr: 50);
        var trailing = Days(TrailStart, 4, rhr: 44);

        var result = MonthlyWellnessSummarizer.Summarize(month, trailing, 31)!;

        var rhr = Metric(result, "restingHeartRate");
        Assert.Equal(50m, rhr.MonthAvg);
        Assert.Null(rhr.TrailingAvg);
        Assert.Equal("insufficient", rhr.Direction);
    }
}
