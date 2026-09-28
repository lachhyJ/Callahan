using Callahan.Api.Services;

namespace Callahan.Api.Tests;

public class StreakTests
{
    private static bool[] W(string weeks) => weeks.Select(c => c == 'x').ToArray();

    [Theory]
    [InlineData("xxx", 3, 3)]      // qualifying right up to this week
    [InlineData("xx.", 2, 2)]      // this week not yet qualified: counts from last week
    [InlineData("x..", 0, 1)]      // last week missed too: streak is broken
    [InlineData("xx.xxx", 3, 3)]   // a gap resets the current run
    [InlineData("xxxx.x", 1, 4)]   // best can exceed current
    [InlineData(".", 0, 0)]
    [InlineData("x", 1, 1)]
    public void CurrentAndBest(string weeks, int current, int best) =>
        Assert.Equal((current, best), WeeklyConsistencyService.Streak(W(weeks)));
}

// The step-taper phase boundaries (decisions: "prescriptive step-taper
// percentages against your own baseline").
public class TaperPhaseCalculatorTests
{
    [Theory]
    [InlineData(11, "build", null)]
    [InlineData(10, "early_taper", 0.75)]
    [InlineData(6, "early_taper", 0.75)]
    [InlineData(5, "peak_taper", 0.5)]
    [InlineData(3, "peak_taper", 0.5)]
    [InlineData(2, "sharpen", 0.25)]
    [InlineData(1, "sharpen", 0.25)]
    [InlineData(0, "game_day", 0.0)]
    public void TenDayTaper(int daysUntil, string phase, double? targetPct)
    {
        var r = TaperPhaseCalculator.Compute(daysUntil, 10, "Nationals");
        Assert.Equal(phase, r.Phase);
        Assert.Equal(targetPct is null ? null : (decimal)targetPct.Value, r.TargetPct);
    }

    [Fact]
    public void PlannedReductionIsTheSharpenCut() => Assert.Equal(0.75m, TaperPhaseCalculator.PlannedReduction(10, null));
}
