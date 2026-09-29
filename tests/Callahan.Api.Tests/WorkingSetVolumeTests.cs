using Callahan.Api.Controllers;
using Callahan.Api.DTOs;
using Callahan.Api.Models;
using Callahan.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;

namespace Callahan.Api.Tests;

// Every volume / set-count read goes through ExerciseSetQueries.WorkingSets, so
// a warmup can't inflate one figure while another leaves it out.
public class WorkingSetVolumeTests : IDisposable
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");
    private readonly Callahan.Api.Data.AppDbContext _db;

    private static readonly DateOnly Day = new(2026, 8, 3);

    public WorkingSetVolumeTests()
    {
        _connection.Open();
        _db = TestData.OpenDb(_connection);
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
        GC.SuppressFinalize(this);
    }

    private int SeedSquatDay()
    {
        var ex = new Exercise { Name = "Squat", Category = ExerciseCategory.Legs };
        ex.MuscleTargets.Add(new ExerciseMuscleTarget { MuscleGroup = MuscleGroup.Quads, IsPrimary = true });
        var session = new WorkoutSession { Date = Day };
        session.Sets.Add(new ExerciseSet { Exercise = ex, SetOrder = 0, WeightKg = 20m, Reps = 10, SetType = SetType.Warmup });
        session.Sets.Add(new ExerciseSet { Exercise = ex, SetOrder = 1, WeightKg = 100m, Reps = 5, SetType = SetType.Normal });
        _db.WorkoutSessions.Add(session);
        _db.SaveChanges();
        return ex.Id;
    }

    [Fact]
    public async Task Taper_gym_volume_excludes_warmups()
    {
        SeedSquatDay();
        Assert.Equal(500m, await TaperBaseline.GymVolumeAsync(_db, Day, Day.AddDays(1)));
    }

    [Fact]
    public async Task Muscle_balance_counts_working_sets_only()
    {
        SeedSquatDay();
        var result = await new MuscleGroupsController(_db).GetBalance(Day, Day);
        var rows = Assert.IsType<OkObjectResult>(result.Result).Value as List<MuscleBalanceEntryDto>;
        Assert.Equal(1m, rows!.Single(r => r.MuscleGroup == "Quads").SetCount);
    }

    [Fact]
    public async Task Exercise_stats_ignore_warmups()
    {
        var id = SeedSquatDay();
        var result = await new ExercisesController(_db).GetStats(id);
        var dto = Assert.IsType<OkObjectResult>(result.Result).Value as ExerciseStatsDto;
        Assert.Equal(500m, dto!.BestSessionVolume);
        Assert.Equal(100m, dto.HeaviestWeightKg);
    }
}
