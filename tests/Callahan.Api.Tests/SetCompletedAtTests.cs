using Callahan.Api.Controllers;
using Callahan.Api.Data;
using Callahan.Api.DTOs;
using Callahan.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Callahan.Api.Tests;

// A set's tick time is stored as UTC and handed back on the detail read; a set
// sent with no time (Live Activity tick, older client) stays null.
public class SetCompletedAtTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly AppDbContext _db;

    public SetCompletedAtTests()
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

    [Fact]
    public async Task Create_stores_tick_time_as_utc_and_GetById_returns_it()
    {
        var bench = new Exercise { Name = "Bench Press", Category = ExerciseCategory.Push };
        _db.Exercises.Add(bench);
        await _db.SaveChangesAsync();
        var controller = new WorkoutSessionsController(_db);

        var utcTick = new DateTime(2026, 11, 6, 0, 15, 30, DateTimeKind.Utc);
        // What System.Text.Json yields for an ISO string carrying an offset.
        var localTick = new DateTimeOffset(2026, 11, 6, 11, 18, 0, TimeSpan.FromHours(11)).LocalDateTime;

        var req = new CreateWorkoutSessionRequest(
            new DateOnly(2026, 11, 6), null, null, null, null, null,
            [
                new CreateExerciseSetRequest(bench.Id, 8, 60m, 0, "Normal", CompletedAt: utcTick),
                new CreateExerciseSetRequest(bench.Id, 8, 60m, 1, "Normal", CompletedAt: localTick),
                new CreateExerciseSetRequest(bench.Id, 8, 60m, 2, "Normal"),
            ],
            null);

        var created = Assert.IsType<OkObjectResult>((await controller.Create(req)).Result).Value as WorkoutSessionDetailDto;
        Assert.NotNull(created);

        var result = await controller.GetById(created!.Id);
        var sets = (Assert.IsType<OkObjectResult>(result.Result).Value as WorkoutSessionDetailDto)!.Sets
            .OrderBy(s => s.SetOrder).ToList();

        Assert.Equal(utcTick, sets[0].CompletedAt);
        Assert.Equal(DateTimeKind.Utc, sets[0].CompletedAt!.Value.Kind);
        Assert.Equal(new DateTime(2026, 11, 6, 0, 18, 0, DateTimeKind.Utc), sets[1].CompletedAt);
        Assert.Null(sets[2].CompletedAt);
    }
}
