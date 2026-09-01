using Callahan.Api.DTOs;
using Callahan.Api.Services;

namespace Callahan.Api.Tests;

public class SeasonStrengthBuilderTests
{
    // Mid-August; a 4-month default window is May..Aug 2026.
    private static readonly DateOnly Today = new(2026, 8, 15);
    private static readonly DateOnly M0 = new(2026, 5, 1);
    private static readonly DateOnly M1 = new(2026, 6, 1);
    private static readonly DateOnly M2 = new(2026, 7, 1);
    private static readonly DateOnly M3 = new(2026, 8, 1);

    // Permissive default: every id 1..100 is a program slot at order 1, so
    // tests that don't care about program filtering behave as before.
    private static readonly Dictionary<int, int> AllPrograms =
        Enumerable.Range(1, 100).ToDictionary(i => i, _ => 1);

    private static SeasonStrengthDto Build(
        IEnumerable<SeasonStrengthBuilder.LiftSetInput>? sets = null,
        IEnumerable<RunLoad>? runs = null,
        IEnumerable<UltimateLoad>? ult = null,
        IEnumerable<SeasonStrengthBuilder.TournamentBand>? tournaments = null,
        IEnumerable<SeasonStrengthBuilder.SeasonInput>? seasons = null,
        IReadOnlyDictionary<int, int>? programOrder = null,
        int months = 4) =>
        SeasonStrengthBuilder.Build(
            Today, months,
            sets ?? [], runs ?? [], ult ?? [], tournaments ?? [], seasons ?? [],
            programOrder ?? AllPrograms);

    private static SeasonStrengthBuilder.LiftSetInput Set(int exerciseId, DateOnly date, int reps, decimal weight, string name = "Squat") =>
        new(exerciseId, name, date, reps, weight);

    [Fact]
    public void DenseMonths_EvenWithNoData()
    {
        var r = Build();

        Assert.Equal(new[] { M0, M1, M2, M3 }, r.Months.Select(m => m.MonthStart));
        Assert.All(r.Months, m => Assert.Equal(0m, m.RunKm));
        Assert.All(r.Months, m => Assert.Equal(0, m.UltimateLivePlayMin));
        Assert.Empty(r.Series);
        Assert.Empty(r.Seasons);
        Assert.Empty(r.Bands);
    }

    [Fact]
    public void Baseline_Month_Is_Zero_Pct_And_LaterMonth_ScalesWithWeight()
    {
        // Same reps each month, so e1RM scales with weight: 100 -> 120 is +20%.
        var r = Build(sets:
        [
            Set(1, new DateOnly(2026, 6, 10), 5, 100m),
            Set(1, new DateOnly(2026, 8, 12), 5, 120m),
        ]);

        var s = Assert.Single(r.Series);
        Assert.Equal(1, s.ExerciseId);
        Assert.Equal(0m, s.Points[0].PctFromBaseline);
        Assert.Equal(20m, s.Points[1].PctFromBaseline);
        Assert.Equal(s.Points[0].E1Rm, s.BaselineE1Rm);
        Assert.Equal(new[] { M1, M3 }, s.Points.Select(p => p.MonthStart));
    }

    [Fact]
    public void MonthlyE1Rm_PicksTheBestSetInTheMonth()
    {
        var r = Build(sets:
        [
            Set(1, new DateOnly(2026, 6, 5), 5, 100m),    // e1RM 116.67
            Set(1, new DateOnly(2026, 6, 20), 3, 110m),   // e1RM 121.0  <- best
            Set(1, new DateOnly(2026, 8, 1), 5, 100m),
        ]);

        var june = Assert.Single(r.Series).Points.Single(p => p.MonthStart == M1);
        Assert.Equal(121.0m, june.E1Rm);
    }

    [Fact]
    public void Exercise_With_Only_One_Populated_Month_Is_Omitted()
    {
        var r = Build(sets:
        [
            Set(1, new DateOnly(2026, 7, 10), 5, 100m),   // one month only
            Set(2, new DateOnly(2026, 6, 10), 5, 100m),
            Set(2, new DateOnly(2026, 8, 10), 5, 105m),
        ]);

        var s = Assert.Single(r.Series);
        Assert.Equal(2, s.ExerciseId);
    }

    [Fact]
    public void Series_SortedBy_ProgramSlot_Then_Name()
    {
        // Movement magnitude no longer decides order — program slot does.
        var r = Build(
            sets:
            [
                Set(10, new DateOnly(2026, 6, 1), 5, 100m, "Big mover"),
                Set(10, new DateOnly(2026, 8, 1), 5, 130m, "Big mover"),   // +30%, slot 3
                Set(20, new DateOnly(2026, 6, 1), 5, 100m, "Small mover"),
                Set(20, new DateOnly(2026, 8, 1), 5, 105m, "Small mover"), // +5%, slot 1
            ],
            programOrder: new Dictionary<int, int> { [10] = 3, [20] = 1 });

        Assert.Equal(new[] { 20, 10 }, r.Series.Select(s => s.ExerciseId));
    }

    [Fact]
    public void Exercise_Not_In_The_Program_Is_Excluded()
    {
        var r = Build(
            sets:
            [
                Set(99, new DateOnly(2026, 6, 1), 5, 100m),
                Set(99, new DateOnly(2026, 8, 1), 5, 120m),
            ],
            programOrder: new Dictionary<int, int> { [1] = 1 });

        Assert.Empty(r.Series);
    }

    [Fact]
    public void IsPrimary_Tracks_ProgramSlot_Depth()
    {
        var r = Build(
            sets:
            [
                Set(1, new DateOnly(2026, 6, 1), 5, 100m, "Compound"),
                Set(1, new DateOnly(2026, 8, 1), 5, 110m, "Compound"),
                Set(2, new DateOnly(2026, 6, 1), 12, 20m, "Isolation"),
                Set(2, new DateOnly(2026, 8, 1), 12, 22m, "Isolation"),
            ],
            programOrder: new Dictionary<int, int> { [1] = 2, [2] = 4 });

        Assert.True(r.Series.Single(s => s.ExerciseId == 1).IsPrimary);
        Assert.False(r.Series.Single(s => s.ExerciseId == 2).IsPrimary);
    }

    [Fact]
    public void Run_And_Ultimate_Load_BucketByMonth()
    {
        var r = Build(
            runs: [new RunLoad(new DateOnly(2026, 6, 3), 5.5m), new RunLoad(new DateOnly(2026, 6, 20), 3.2m)],
            ult: [new UltimateLoad(new DateOnly(2026, 7, 5), 1800), new UltimateLoad(new DateOnly(2026, 7, 19), 900)]);

        Assert.Equal(8.7m, r.Months.Single(m => m.MonthStart == M1).RunKm);
        Assert.Equal(45, r.Months.Single(m => m.MonthStart == M2).UltimateLivePlayMin);   // 2700s -> 45 min
    }

    [Fact]
    public void DataOutsideTheWindow_IsIgnored()
    {
        var r = Build(
            sets:
            [
                Set(1, new DateOnly(2026, 3, 1), 5, 100m),
                Set(1, new DateOnly(2026, 3, 20), 5, 200m),
            ],
            runs: [new RunLoad(new DateOnly(2026, 3, 10), 99m)]);

        Assert.Empty(r.Series);
        Assert.All(r.Months, m => Assert.Equal(0m, m.RunKm));
    }

    [Fact]
    public void SeasonBand_IsClippedToWindow_And_TargetDatePassesThrough()
    {
        var r = Build(seasons:
        [
            new SeasonStrengthBuilder.SeasonInput("2026 Season", new DateOnly(2026, 4, 15), new DateOnly(2026, 9, 20), new DateOnly(2026, 7, 10)),
        ]);

        var band = Assert.Single(r.Seasons);
        Assert.Equal("2026 Season", band.Name);
        Assert.Equal(M0, band.Start);                       // clipped up from 15 Apr
        Assert.Equal(new DateOnly(2026, 8, 31), band.End);  // clipped down from 20 Sep
        Assert.Equal(new DateOnly(2026, 7, 10), band.TargetDate);
    }

    [Fact]
    public void SeasonBand_TargetOutsideWindow_IsNull()
    {
        var r = Build(seasons:
        [
            new SeasonStrengthBuilder.SeasonInput("S", new DateOnly(2026, 6, 1), new DateOnly(2026, 7, 15), new DateOnly(2026, 12, 1)),
        ]);

        Assert.Null(Assert.Single(r.Seasons).TargetDate);
    }

    [Fact]
    public void TournamentBands_FilteredToWindow()
    {
        var r = Build(tournaments:
        [
            new SeasonStrengthBuilder.TournamentBand("Regionals", new DateOnly(2026, 7, 4), new DateOnly(2026, 7, 6)),
            new SeasonStrengthBuilder.TournamentBand("Winter Cup", new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 2)),
        ]);

        var band = Assert.Single(r.Bands);
        Assert.Equal("Regionals", band.Name);
    }
}

public class LiftMathTests
{
    [Fact]
    public void Epley1Rm_MatchesFormula()
    {
        Assert.Equal(120m, LiftMath.Epley1Rm(0, 120m));
        Assert.Equal(121.0m, LiftMath.Epley1Rm(3, 110m));   // 110 * 1.1
        Assert.Equal(120m, LiftMath.Epley1Rm(10, 90m));     // 90 * (1 + 10/30) = 90 * 4/3
    }
}
