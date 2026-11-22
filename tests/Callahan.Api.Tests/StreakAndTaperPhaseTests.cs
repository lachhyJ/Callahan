using Callahan.Api.Models;
using Callahan.Api.Services;

namespace Callahan.Api.Tests;

// The taper phase boundaries, one shape per v4 taper profile.
public class TaperPhaseCalculatorTests
{
    [Theory]
    [InlineData(6, "build", null)]
    [InlineData(5, "taper", 0.7)]
    [InlineData(1, "taper", 0.7)]
    [InlineData(0, "game_day", 0.0)]
    public void RegionalsIsOneThirtyPercentStep(int daysUntil, string phase, double? targetPct)
    {
        var r = TaperPhaseCalculator.Compute(daysUntil, 5, TaperProfile.Regionals, "VUC");
        Assert.Equal(phase, r.Phase);
        Assert.Equal(targetPct is null ? null : (decimal)targetPct.Value, r.TargetPct);
    }

    [Fact]
    public void RegionalsScalesEveryExerciseAlike()
    {
        var r = TaperPhaseCalculator.Compute(3, 5, TaperProfile.Regionals, null);
        Assert.Equal(0.7m, r.KeepPct);
        Assert.Equal(0.7m, r.AccessoryPct);
    }

    [Theory]
    [InlineData(15, "build", null, null, null)]
    [InlineData(14, "taper", 0.7, 1.0, 0.0)]
    [InlineData(8, "taper", 0.7, 1.0, 0.0)]
    [InlineData(7, "final_days", 0.5, 0.5, 0.0)]
    [InlineData(1, "final_days", 0.5, 0.5, 0.0)]
    [InlineData(0, "game_day", 0.0, null, null)]
    public void NationalsCutsAccessoriesThenHalvesTheFinalWeek(int daysUntil, string phase, double? targetPct, double? keepPct, double? accessoryPct)
    {
        var r = TaperPhaseCalculator.Compute(daysUntil, 14, TaperProfile.Nationals, "AUC");
        Assert.Equal(phase, r.Phase);
        Assert.Equal(targetPct is null ? null : (decimal)targetPct.Value, r.TargetPct);
        Assert.Equal(keepPct is null ? null : (decimal)keepPct.Value, r.KeepPct);
        Assert.Equal(accessoryPct is null ? null : (decimal)accessoryPct.Value, r.AccessoryPct);
    }

    [Fact]
    public void PlannedReductionIsTheDeepestCut()
    {
        Assert.Equal(0.3m, TaperPhaseCalculator.PlannedReduction(TaperProfile.Regionals));
        Assert.Equal(0.5m, TaperPhaseCalculator.PlannedReduction(TaperProfile.Nationals));
    }

    [Theory]
    [InlineData("Nationals", TaperProfile.Nationals)]
    [InlineData("regionals", TaperProfile.Regionals)]
    [InlineData("None", null)]
    [InlineData("", null)]
    [InlineData(null, null)]
    [InlineData("2", null)]
    public void ParseAcceptsNamesOnly(string? value, TaperProfile? expected) =>
        Assert.Equal(expected, TaperPhaseCalculator.Parse(value));
}
