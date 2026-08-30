using Callahan.Api.DTOs;
using Callahan.Api.Services;

namespace Callahan.Api.Tests;

public class MonthlyReportHeadlineTests
{
    private static ConsistencySectionDto Consistency(decimal perWeek, decimal trailingPerWeek) =>
        new(TotalSessions: (int)Math.Round(perWeek * 4), WeeksInMonth: 4m,
            SessionsPerWeek: perWeek, TrailingSessionsPerWeek: trailingPerWeek,
            SessionsByType: [], WeeklyTargets: [], DaysTrained: 0, DaysInMonth: 30);

    private static LoadProgressionSectionDto Load(
        int prs = 0, int stalls = 0, decimal moverDelta = 0m)
    {
        var prList = Enumerable.Range(0, prs)
            .Select(i => new PrDto(i, $"Ex{i}", 100m, new DateOnly(2026, 8, 1), null)).ToList();
        var stallList = Enumerable.Range(0, stalls)
            .Select(i => new StallDto(i, $"Ex{i}", 5, new DateOnly(2026, 8, 1))).ToList();
        var movers = moverDelta == 0m
            ? new List<MoverDto>()
            : [new MoverDto(1, "Ex", 100m, 100m + moverDelta, moverDelta)];
        return new LoadProgressionSectionDto(prList, movers, stallList, []);
    }

    [Fact]
    public void StrongMonth_WhenPaceHeldAndBarMovingWithNoStalls()
    {
        var verdict = MonthlyReportBuilder.ClassifyMonth(Consistency(2.5m, 2.0m), Load(prs: 2, moverDelta: 5m));
        Assert.Equal("Strong month", verdict);
    }

    [Fact]
    public void DownMonth_WhenWellUnderTrailingRate()
    {
        var verdict = MonthlyReportBuilder.ClassifyMonth(Consistency(1.2m, 2.0m), Load(prs: 1));
        Assert.Equal("Down month", verdict);
    }

    [Fact]
    public void SteadyMonth_WhenStallsPileUpButPaceHeld()
    {
        // A high-volume month full of plateaus is Steady, not Down — only a
        // real drop in training frequency makes a month "Down".
        var verdict = MonthlyReportBuilder.ClassifyMonth(Consistency(2.5m, 2.0m), Load(stalls: 3, prs: 1));
        Assert.Equal("Steady month", verdict);
    }

    [Fact]
    public void SteadyMonth_WhenPaceHeldButNothingMoving()
    {
        var verdict = MonthlyReportBuilder.ClassifyMonth(Consistency(2.0m, 2.0m), Load());
        Assert.Equal("Steady month", verdict);
    }

    [Fact]
    public void SteadyMonth_WhenSlightlyBelowTrailingButNotDown()
    {
        // 1.8 vs 2.0 — down but not past the 0.8x floor, no stall pile.
        var verdict = MonthlyReportBuilder.ClassifyMonth(Consistency(1.8m, 2.0m), Load(prs: 1));
        Assert.Equal("Steady month", verdict);
    }

    [Fact]
    public void FirstMonth_WithNoTrailingHistory_IsNotDown()
    {
        var verdict = MonthlyReportBuilder.ClassifyMonth(Consistency(2.0m, 0m), Load(prs: 3));
        Assert.NotEqual("Down month", verdict);
    }
}
