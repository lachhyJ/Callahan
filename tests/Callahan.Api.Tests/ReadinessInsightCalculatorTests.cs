using Callahan.Api.DTOs;
using Callahan.Api.Services;

namespace Callahan.Api.Tests;

public class ReadinessInsightCalculatorTests
{
    // Only the four fields the calculator reads; everything else stays null.
    private static DailyWellnessDto Row(
        DateOnly date, int? readiness = null, int? sleepScore = null, int? sleepSeconds = null, int? hrv = null) =>
        new(0, date, sleepSeconds, null, null, null, null, sleepScore, null, hrv, null, null, readiness, null, null, null, null, null, null);

    private static List<DailyWellnessDto> Baseline(
        int days, int? readiness = null, int? sleepScore = null, int? sleepSeconds = null, int? hrv = null)
    {
        var start = new DateOnly(2026, 8, 1);
        return Enumerable.Range(0, days)
            .Select(i => Row(start.AddDays(i), readiness, sleepScore, sleepSeconds, hrv))
            .ToList();
    }

    private static readonly DateOnly Today = new(2026, 9, 1);

    private static MetricInsightDto Metric(ReadinessInsightDto r, string key) => r.Metrics.Single(m => m.Key == key);

    [Fact]
    public void InsufficientHistory_WhenFewerThanSevenBaselineDays()
    {
        var baseline = Baseline(3, readiness: 70, sleepScore: 80, sleepSeconds: 27000, hrv: 50);
        var today = Row(Today, readiness: 40, sleepScore: 50, sleepSeconds: 20000, hrv: 35);

        var r = ReadinessInsightCalculator.Compute(today, baseline);

        Assert.False(r.HasEnoughHistory);
        Assert.Equal("Not enough wellness history yet.", r.Headline);
        Assert.All(r.Metrics, m => Assert.Equal("insufficient", m.Direction));
        Assert.Equal("Not enough history yet", Metric(r, "readiness").Phrase);
    }

    [Fact]
    public void InLine_WhenTodayMatchesFlatBaseline()
    {
        var baseline = Baseline(14, readiness: 68, sleepScore: 80, sleepSeconds: 27000, hrv: 50);
        var today = Row(Today, readiness: 68, sleepScore: 80, sleepSeconds: 27000, hrv: 50);

        var r = ReadinessInsightCalculator.Compute(today, baseline);

        Assert.True(r.HasEnoughHistory);
        Assert.Equal("You're tracking in line with your recent average.", r.Headline);
        var readiness = Metric(r, "readiness");
        Assert.Equal("in_line", readiness.Direction);
        Assert.Equal(68d, readiness.BaselineAvg);
        Assert.Equal(14, readiness.BaselineDays);
    }

    [Fact]
    public void Below_WhenReadinessWellUnderBaseline()
    {
        var baseline = Baseline(10, readiness: 70, sleepScore: 80, sleepSeconds: 27000, hrv: 50);
        var today = Row(Today, readiness: 45, sleepScore: 80, sleepSeconds: 27000, hrv: 50);

        var r = ReadinessInsightCalculator.Compute(today, baseline);

        var readiness = Metric(r, "readiness");
        Assert.Equal("below", readiness.Direction);
        Assert.Contains("well below", readiness.Phrase);
        Assert.Equal(45d, readiness.Today);
        Assert.Equal(70d, readiness.BaselineAvg);
        Assert.StartsWith("Readiness is below your recent average", r.Headline);
        Assert.Contains("more tired than usual", r.Headline);
    }

    [Fact]
    public void CombinesNounsAndDedupesSleep_WhenReadinessAndBothSleepMetricsDown()
    {
        var baseline = Baseline(10, readiness: 70, sleepScore: 85, sleepSeconds: 28000, hrv: 50);
        // readiness -20 (strong), sleep score -20 (strong), sleep duration -100 min (strong), hrv flat
        var today = Row(Today, readiness: 50, sleepScore: 65, sleepSeconds: 22000, hrv: 50);

        var r = ReadinessInsightCalculator.Compute(today, baseline);

        Assert.Equal("Readiness and sleep are below your recent average — more tired than usual.", r.Headline);
    }

    [Fact]
    public void MixedPicture_WhenSleepDownAndHrvUp()
    {
        var baseline = Baseline(10, readiness: 68, sleepScore: 80, sleepSeconds: 27000, hrv: 45);
        // sleep duration -66 min (strong below), hrv +33% (strong above), readiness/sleepScore flat
        var today = Row(Today, readiness: 68, sleepScore: 80, sleepSeconds: 23000, hrv: 60);

        var r = ReadinessInsightCalculator.Compute(today, baseline);

        Assert.Equal("below", Metric(r, "sleepDuration").Direction);
        Assert.Equal("above", Metric(r, "hrv").Direction);
        Assert.Contains("down,", r.Headline);
        Assert.Contains("up — a mixed picture", r.Headline);
    }

    [Fact]
    public void NullTodayMetric_IsExcludedFromHeadline()
    {
        var baseline = Baseline(10, readiness: 70, sleepScore: 85, sleepSeconds: 27000, hrv: 50);
        var today = Row(Today, readiness: null, sleepScore: 65, sleepSeconds: 27000, hrv: 50);

        var r = ReadinessInsightCalculator.Compute(today, baseline);

        var readiness = Metric(r, "readiness");
        Assert.Equal("insufficient", readiness.Direction);
        Assert.Equal("No reading today", readiness.Phrase);
        Assert.DoesNotContain("eadiness", r.Headline);
        Assert.StartsWith("Sleep is below", r.Headline);
    }

    [Fact]
    public void SparseBaseline_CountsOnlyNonNullDays()
    {
        // 10 rows: readiness on only 5 of them, sleep score on all 10.
        var start = new DateOnly(2026, 8, 1);
        var baseline = Enumerable.Range(0, 10)
            .Select(i => Row(start.AddDays(i), readiness: i < 5 ? 70 : (int?)null, sleepScore: 85))
            .ToList();
        var today = Row(Today, readiness: 45, sleepScore: 65);

        var r = ReadinessInsightCalculator.Compute(today, baseline);

        var readiness = Metric(r, "readiness");
        Assert.Equal(5, readiness.BaselineDays);
        Assert.Equal("insufficient", readiness.Direction);
        // sleep score still has a full window and reads below
        Assert.Equal("below", Metric(r, "sleepScore").Direction);
        Assert.True(r.HasEnoughHistory);
    }

    [Theory]
    [InlineData(70, "in_line")]   // +2, within ±5
    [InlineData(64, "in_line")]   // -4, within ±5
    [InlineData(62, "below")]     // -6, past the in-line band
    [InlineData(55, "below")]     // -13, strong below
    [InlineData(74, "above")]     // +6, mild above
    [InlineData(85, "above")]     // +17, strong above
    public void PointBands_ClassifyReadinessDelta(int todayReadiness, string expected)
    {
        var baseline = Baseline(10, readiness: 68);
        var r = ReadinessInsightCalculator.Compute(Row(Today, readiness: todayReadiness), baseline);
        Assert.Equal(expected, Metric(r, "readiness").Direction);
    }
}
