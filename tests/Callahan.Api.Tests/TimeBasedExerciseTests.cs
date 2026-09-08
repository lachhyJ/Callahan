using Callahan.Api.Controllers;
using Callahan.Api.Data;
using Callahan.Api.DTOs;
using Callahan.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Callahan.Api.Tests;

// Time-based exercises log a hold in seconds, not a rep count. The invariants
// under test: the save path stores such a set as DurationSeconds != null with
// Reps/WeightKg pinned to 0 (nothing downstream may read a count off it), the
// detail read hands DurationSeconds back, a negative duration is refused, and
// every strength/volume read excludes the row so a 0/0 set can't drag a Max or
// flip an exercise's basis.
public class TimeBasedExerciseTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly AppDbContext _db;

    private static readonly DateOnly Aug = new(2026, 8, 3);

    public TimeBasedExerciseTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options;
        _db = new AppDbContext(options);
        _db.Database.EnsureCreated();
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
        GC.SuppressFinalize(this);
    }

    private Exercise AddExercise(string name, ExerciseCategory category, bool timeBased = false, bool perSide = false)
    {
        var ex = new Exercise { Name = name, Category = category, IsTimeBased = timeBased, IsPerSide = perSide };
        _db.Exercises.Add(ex);
        _db.SaveChanges();
        return ex;
    }

    private WorkoutSession AddSession(DateOnly date, params ExerciseSet[] sets)
    {
        var session = new WorkoutSession { Date = date };
        foreach (var s in sets) session.Sets.Add(s);
        _db.WorkoutSessions.Add(session);
        _db.SaveChanges();
        return session;
    }

    private static ExerciseSet Normal(int exerciseId, int order, decimal weightKg, int reps) => new()
    {
        ExerciseId = exerciseId,
        SetOrder = order,
        WeightKg = weightKg,
        Reps = reps,
        SetType = SetType.Normal,
    };

    private static ExerciseSet Timed(int exerciseId, int order, int seconds) => new()
    {
        ExerciseId = exerciseId,
        SetOrder = order,
        WeightKg = 0m,
        Reps = 0,
        DurationSeconds = seconds,
        SetType = SetType.Normal,
    };

    // ---- save path -------------------------------------------------------

    [Fact]
    public async Task Create_pins_reps_and_weight_to_zero_on_a_time_set()
    {
        var plank = AddExercise("Copenhagen Plank", ExerciseCategory.Core, timeBased: true, perSide: true);
        var controller = new WorkoutSessionsController(_db);

        // The client still sends whatever was in the (hidden) rep field - the
        // server must not trust it.
        var req = new CreateWorkoutSessionRequest(
            Aug, "Core", null, null, null, null,
            [new CreateExerciseSetRequest(plank.Id, Reps: 12, WeightKg: 40m, SetOrder: 0, SetType: "Normal", DurationSeconds: 20)],
            null);

        var result = await controller.Create(req);
        var created = Assert.IsType<OkObjectResult>(result.Result).Value as WorkoutSessionDetailDto;
        Assert.NotNull(created);

        var stored = _db.ExerciseSets.Single(s => s.WorkoutSessionId == created!.Id);
        Assert.Equal(20, stored.DurationSeconds);
        Assert.Equal(0, stored.Reps);
        Assert.Equal(0m, stored.WeightKg);
    }

    [Fact]
    public async Task Create_rejects_a_negative_duration()
    {
        var plank = AddExercise("Copenhagen Plank", ExerciseCategory.Core, timeBased: true);
        var controller = new WorkoutSessionsController(_db);

        var req = new CreateWorkoutSessionRequest(
            Aug, null, null, null, null, null,
            [new CreateExerciseSetRequest(plank.Id, 0, 0m, 0, "Normal", DurationSeconds: -5)],
            null);

        var result = await controller.Create(req);
        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task GetById_round_trips_DurationSeconds()
    {
        var bench = AddExercise("Bench Press", ExerciseCategory.Push);
        var plank = AddExercise("Copenhagen Plank", ExerciseCategory.Core, timeBased: true);
        var session = AddSession(Aug, Normal(bench.Id, 0, 60m, 8), Timed(plank.Id, 0, 25));
        var controller = new WorkoutSessionsController(_db);

        var result = await controller.GetById(session.Id);
        var dto = Assert.IsType<OkObjectResult>(result.Result).Value as WorkoutSessionDetailDto;
        Assert.NotNull(dto);

        var benchDto = dto!.Sets.Single(s => s.ExerciseName == "Bench Press");
        var plankDto = dto.Sets.Single(s => s.ExerciseName == "Copenhagen Plank");
        Assert.Null(benchDto.DurationSeconds);
        Assert.Equal(25, plankDto.DurationSeconds);
    }

    // ---- strength / volume exclusion -----------------------------------

    [Fact]
    public async Task GetStats_ignores_time_sets_when_a_lift_also_has_real_sets()
    {
        var ex = AddExercise("Weighted Plank", ExerciseCategory.Core);
        AddSession(Aug, Normal(ex.Id, 0, 100m, 5), Normal(ex.Id, 1, 100m, 5));
        AddSession(Aug.AddDays(7), Timed(ex.Id, 0, 45)); // a pure-hold session

        var controller = new ExercisesController(_db);
        var result = await controller.GetStats(ex.Id);
        var dto = Assert.IsType<OkObjectResult>(result.Result).Value as ExerciseStatsDto;
        Assert.NotNull(dto);

        // Only the real session contributes a chart point; the hold-only
        // session is skipped rather than plotted at weight 0.
        Assert.Single(dto!.Chart);
        Assert.Equal(100m, dto.HeaviestWeightKg);
    }

    [Fact]
    public async Task GetStats_on_a_purely_time_based_exercise_is_empty()
    {
        var plank = AddExercise("Copenhagen Plank", ExerciseCategory.Core, timeBased: true);
        AddSession(Aug, Timed(plank.Id, 0, 20), Timed(plank.Id, 1, 20));

        var controller = new ExercisesController(_db);
        var result = await controller.GetStats(plank.Id);
        var dto = Assert.IsType<OkObjectResult>(result.Result).Value as ExerciseStatsDto;
        Assert.NotNull(dto);

        Assert.Empty(dto!.Chart);
        Assert.Equal(0m, dto.HeaviestWeightKg);
        Assert.Equal(0m, dto.BestEstimated1Rm);
        Assert.Equal(0m, dto.BestSetVolume);
    }

    [Fact]
    public async Task GetTrends_volume_counts_only_real_sets()
    {
        var bench = AddExercise("Bench Press", ExerciseCategory.Push);
        var plank = AddExercise("Copenhagen Plank", ExerciseCategory.Core, timeBased: true);
        AddSession(Aug, Normal(bench.Id, 0, 50m, 10), Timed(plank.Id, 0, 30));

        var controller = new TrendsController(_db);
        var result = await controller.GetTrends(months: 3);
        var points = Assert.IsType<OkObjectResult>(result.Result).Value as List<TrendPointDto>;
        Assert.NotNull(points);

        // 50 * 10 from the one real set, and nothing from the hold.
        Assert.Equal(500m, points!.Sum(p => p.VolumeKg));
    }
}
