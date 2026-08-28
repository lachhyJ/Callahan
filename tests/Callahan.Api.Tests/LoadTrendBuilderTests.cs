using Callahan.Api.DTOs;
using Callahan.Api.Services;

namespace Callahan.Api.Tests;

public class LoadTrendBuilderTests
{
    // A Wednesday — MondayOf is 2026-08-24.
    private static readonly DateOnly Today = new(2026, 8, 26);
    private static readonly DateOnly Wk0 = new(2026, 8, 3);
    private static readonly DateOnly Wk1 = new(2026, 8, 10);
    private static readonly DateOnly Wk2 = new(2026, 8, 17);
    private static readonly DateOnly Wk3 = new(2026, 8, 24);

    private static DailyWellnessDto Wellness(DateOnly date, int? readiness = null, int? hrv = null, int? sleepScore = null) =>
        new(0, date, null, null, null, null, null, sleepScore, null, hrv, null, null, readiness, null, null, null, null, null, null);

    private static List<LoadTrendWeekDto> Build(
        IEnumerable<GymSetLoad>? gym = null,
        IEnumerable<RunLoad>? runs = null,
        IEnumerable<UltimateLoad>? ult = null,
        IEnumerable<DailyWellnessDto>? wellness = null,
        IEnumerable<TournamentSpan>? tournaments = null,
        int weeks = 4) =>
        LoadTrendBuilder.Build(
            Today, weeks,
            gym ?? [], runs ?? [], ult ?? [], wellness ?? [], tournaments ?? []);

    [Fact]
    public void DenseOutput_EvenWithNoData()
    {
        var r = Build();

        Assert.Equal(4, r.Count);
        Assert.Equal(new[] { Wk0, Wk1, Wk2, Wk3 }, r.Select(w => w.WeekStart));
        Assert.All(r, w =>
        {
            Assert.Equal(0m, w.GymVolume);
            Assert.Equal(0m, w.RunKm);
            Assert.Equal(0, w.UltimateLivePlayMin);
            Assert.Null(w.MeanReadiness);
            Assert.Null(w.MeanHrv);
            Assert.Null(w.MeanSleepScore);
            Assert.False(w.IsTournamentWeek);
        });
    }

    [Fact]
    public void PartialCurrentWeek_IsTheLastBucket()
    {
        var r = Build();
        Assert.Equal(Wk3, r[^1].WeekStart);
    }

    [Fact]
    public void GymVolume_BucketedByMondayWeek()
    {
        var r = Build(gym:
        [
            new GymSetLoad(new DateOnly(2026, 8, 5), 100m),   // Wk0
            new GymSetLoad(new DateOnly(2026, 8, 11), 50m),   // Wk1
            new GymSetLoad(new DateOnly(2026, 8, 13), 25m),   // Wk1
            new GymSetLoad(new DateOnly(2026, 8, 24), 200m),  // Wk3
        ]);

        Assert.Equal(100m, r.Single(w => w.WeekStart == Wk0).GymVolume);
        Assert.Equal(75m, r.Single(w => w.WeekStart == Wk1).GymVolume);
        Assert.Equal(0m, r.Single(w => w.WeekStart == Wk2).GymVolume);
        Assert.Equal(200m, r.Single(w => w.WeekStart == Wk3).GymVolume);
    }

    [Fact]
    public void RunKm_And_UltimateMinutes_Convert()
    {
        var r = Build(
            runs: [new RunLoad(new DateOnly(2026, 8, 12), 5.5m), new RunLoad(new DateOnly(2026, 8, 14), 3.2m)],
            ult: [new UltimateLoad(new DateOnly(2026, 8, 12), 1800), new UltimateLoad(new DateOnly(2026, 8, 15), 900)]);

        var wk1 = r.Single(w => w.WeekStart == Wk1);
        Assert.Equal(8.7m, wk1.RunKm);
        Assert.Equal(45, wk1.UltimateLivePlayMin);   // 2700s → 45 min
    }

    [Fact]
    public void WellnessMean_PerWeek_RoundedOneDp()
    {
        var r = Build(wellness:
        [
            Wellness(new DateOnly(2026, 8, 11), readiness: 60, hrv: 48),
            Wellness(new DateOnly(2026, 8, 13), readiness: 71, hrv: 52),
            Wellness(new DateOnly(2026, 8, 24), readiness: 40),
        ]);

        var wk1 = r.Single(w => w.WeekStart == Wk1);
        Assert.Equal(65.5, wk1.MeanReadiness);
        Assert.Equal(50.0, wk1.MeanHrv);
        Assert.Null(wk1.MeanSleepScore);

        Assert.Equal(40.0, r.Single(w => w.WeekStart == Wk3).MeanReadiness);
        Assert.Null(r.Single(w => w.WeekStart == Wk0).MeanReadiness);
    }

    [Fact]
    public void TournamentSpanningWeekBoundary_MarksEveryOverlappedWeek()
    {
        var r = Build(tournaments: [new TournamentSpan(new DateOnly(2026, 8, 8), new DateOnly(2026, 8, 10))]);

        Assert.True(r.Single(w => w.WeekStart == Wk0).IsTournamentWeek);   // 8, 9 Aug
        Assert.True(r.Single(w => w.WeekStart == Wk1).IsTournamentWeek);   // 10 Aug
        Assert.False(r.Single(w => w.WeekStart == Wk2).IsTournamentWeek);
    }

    [Fact]
    public void DataOutsideTheWindow_IsIgnored()
    {
        var r = Build(
            gym: [new GymSetLoad(new DateOnly(2026, 7, 20), 999m)],           // before Wk0
            wellness: [Wellness(new DateOnly(2026, 7, 20), readiness: 99)]);

        Assert.Equal(4, r.Count);
        Assert.All(r, w => Assert.Equal(0m, w.GymVolume));
        Assert.All(r, w => Assert.Null(w.MeanReadiness));
    }
}
