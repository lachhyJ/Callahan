using Callahan.Api.DTOs;
using Callahan.Api.Services;

namespace Callahan.Api.Tests;

public class UltimateDistanceBuilderTests
{
    // Mid-August; a 3-month window is Jun / Jul / Aug 2026.
    private static readonly DateOnly Today = new(2026, 8, 15);
    private static readonly DateOnly Jun = new(2026, 6, 1);
    private static readonly DateOnly Jul = new(2026, 7, 1);
    private static readonly DateOnly Aug = new(2026, 8, 1);

    private static List<UltimateDistanceMonthDto> Build(
        IEnumerable<UltimateDistanceActivity>? ultimate = null,
        IEnumerable<RunLoad>? runs = null,
        int months = 3) =>
        UltimateDistanceBuilder.Build(Today, months, ultimate ?? [], runs ?? []);

    private static UltimateDistanceActivity Ult(DateOnly date, decimal? km, string type = "Game") => new(date, km, type);

    [Fact]
    public void DenseOutput_EvenWithNoData()
    {
        var r = Build();

        Assert.Equal(new[] { Jun, Jul, Aug }, r.Select(m => m.MonthStart));
        Assert.All(r, m =>
        {
            Assert.Equal(0m, m.UltimateKm);
            Assert.Equal(0m, m.RunKm);
            Assert.Equal(0, m.UltimateSessionsWithoutDistance);
            Assert.Empty(m.ByType);
        });
    }

    [Fact]
    public void CurrentMonth_IsTheLastBucket()
    {
        Assert.Equal(Aug, Build()[^1].MonthStart);
    }

    [Fact]
    public void Distance_BucketedByCalendarMonth()
    {
        var r = Build(ultimate:
        [
            Ult(new DateOnly(2026, 6, 30), 3.0m),
            Ult(new DateOnly(2026, 7, 1), 4.0m),
            Ult(new DateOnly(2026, 7, 20), 2.5m),
            Ult(new DateOnly(2026, 8, 15), 5.0m),
        ]);

        Assert.Equal(3.0m, r.Single(m => m.MonthStart == Jun).UltimateKm);
        Assert.Equal(6.5m, r.Single(m => m.MonthStart == Jul).UltimateKm);
        Assert.Equal(5.0m, r.Single(m => m.MonthStart == Aug).UltimateKm);
    }

    [Fact]
    public void ByType_AttributedToPrimary_And_SumsToMonthTotal()
    {
        var aug = Build(ultimate:
        [
            Ult(new DateOnly(2026, 8, 2), 3.20m, "Game"),
            Ult(new DateOnly(2026, 8, 3), 4.30m, "Game"),
            Ult(new DateOnly(2026, 8, 9), 7.71m, "Pod"),
            Ult(new DateOnly(2026, 8, 26), 0.32m, "Throws"),
        ]).Single(m => m.MonthStart == Aug);

        Assert.Equal(15.53m, aug.UltimateKm);
        Assert.Equal(aug.UltimateKm, aug.ByType.Sum(t => t.Km));

        var game = aug.ByType.Single(t => t.TypeName == "Game");
        Assert.Equal(7.50m, game.Km);
        Assert.Equal(2, game.Sessions);
    }

    [Fact]
    public void ByType_SortedByKmDescending_ThenSessions_ThenName()
    {
        var aug = Build(ultimate:
        [
            Ult(new DateOnly(2026, 8, 2), 2.0m, "Pod"),
            Ult(new DateOnly(2026, 8, 3), 9.0m, "Game"),
            Ult(new DateOnly(2026, 8, 4), 2.0m, "Club Training"),
        ]).Single(m => m.MonthStart == Aug);

        Assert.Equal(new[] { "Game", "Club Training", "Pod" }, aug.ByType.Select(t => t.TypeName));
    }

    [Fact]
    public void NullDistanceSessions_CountedNotSummed_AtBothLevels()
    {
        var aug = Build(ultimate:
        [
            Ult(new DateOnly(2026, 8, 2), 3.0m, "Game"),
            Ult(new DateOnly(2026, 8, 6), null, "Solo"),
            Ult(new DateOnly(2026, 8, 7), null, "Solo"),
            Ult(new DateOnly(2026, 8, 8), null, "Game"),
        ]).Single(m => m.MonthStart == Aug);

        Assert.Equal(3.0m, aug.UltimateKm);
        Assert.Equal(3, aug.UltimateSessionsWithoutDistance);

        var solo = aug.ByType.Single(t => t.TypeName == "Solo");
        Assert.Equal(0m, solo.Km);
        Assert.Equal(2, solo.Sessions);
        Assert.Equal(2, solo.SessionsWithoutDistance);

        var game = aug.ByType.Single(t => t.TypeName == "Game");
        Assert.Equal(2, game.Sessions);
        Assert.Equal(1, game.SessionsWithoutDistance);
    }

    [Fact]
    public void RunKm_TotalledAlongside_RoundedTwoDp()
    {
        var r = Build(runs:
        [
            new RunLoad(new DateOnly(2026, 7, 5), 5.123m),
            new RunLoad(new DateOnly(2026, 7, 25), 3.1m),
            new RunLoad(new DateOnly(2026, 8, 1), 4.0m),
        ]);

        Assert.Equal(8.22m, r.Single(m => m.MonthStart == Jul).RunKm);
        Assert.Equal(4.0m, r.Single(m => m.MonthStart == Aug).RunKm);
        Assert.Equal(0m, r.Single(m => m.MonthStart == Jun).RunKm);
    }

    [Fact]
    public void DataOutsideTheWindow_IsIgnored()
    {
        var r = Build(
            ultimate: [Ult(new DateOnly(2026, 5, 31), 99m), Ult(new DateOnly(2026, 9, 1), 99m)],
            runs: [new RunLoad(new DateOnly(2026, 5, 1), 99m)]);

        Assert.Equal(3, r.Count);
        Assert.All(r, m => Assert.Equal(0m, m.UltimateKm));
        Assert.All(r, m => Assert.Equal(0m, m.RunKm));
    }

    [Fact]
    public void MonthTotal_RoundedOnce_FromRawAccumulators()
    {
        // Three sessions that each round down at 2dp individually (x.xx4) but
        // whose raw sum rounds up - the month total must follow the raw sum.
        var aug = Build(ultimate:
        [
            Ult(new DateOnly(2026, 8, 2), 1.004m, "Game"),
            Ult(new DateOnly(2026, 8, 3), 1.004m, "Game"),
            Ult(new DateOnly(2026, 8, 4), 1.004m, "Pod"),
        ]).Single(m => m.MonthStart == Aug);

        Assert.Equal(3.01m, aug.UltimateKm);
    }
}
