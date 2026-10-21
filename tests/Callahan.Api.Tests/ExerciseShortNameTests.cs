using Callahan.Api.Controllers;
using Callahan.Api.Data;
using Callahan.Api.DTOs;
using Callahan.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Callahan.Api.Tests;

// ShortName is the label the Garmin field shows when Name runs long. Null means
// "use Name", so the invariants are: a blank or whitespace value is stored as
// null (never an empty string the client would mistake for a real label), it is
// trimmed, an unknown exercise is a 404, and the stats read hands it back.
public class ExerciseShortNameTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly AppDbContext _db;

    public ExerciseShortNameTests()
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

    private Exercise AddExercise(string name)
    {
        var ex = new Exercise { Name = name, Category = ExerciseCategory.Legs };
        _db.Exercises.Add(ex);
        _db.SaveChanges();
        return ex;
    }

    [Fact]
    public async Task UpdateShortName_TrimsAndStores()
    {
        var ex = AddExercise("Single Leg Standing Calf Raise");

        var result = await new ExercisesController(_db).UpdateShortName(ex.Id, new UpdateExerciseShortNameRequestDto("  1-Leg Calf Raise "));

        Assert.IsType<NoContentResult>(result);
        Assert.Equal("1-Leg Calf Raise", (await _db.Exercises.AsNoTracking().SingleAsync(e => e.Id == ex.Id)).ShortName);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task UpdateShortName_BlankClearsToNull(string? value)
    {
        var ex = AddExercise("Triceps Rope Pushdown");
        ex.ShortName = "Rope Pushdown";
        _db.SaveChanges();

        await new ExercisesController(_db).UpdateShortName(ex.Id, new UpdateExerciseShortNameRequestDto(value));

        Assert.Null((await _db.Exercises.AsNoTracking().SingleAsync(e => e.Id == ex.Id)).ShortName);
    }

    [Fact]
    public async Task UpdateShortName_UnknownExercise_IsNotFound()
    {
        var result = await new ExercisesController(_db).UpdateShortName(9999, new UpdateExerciseShortNameRequestDto("x"));

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task GetStats_ReturnsShortName()
    {
        var ex = AddExercise("Cable Fly Crossovers");
        ex.ShortName = "Cable Fly";
        _db.SaveChanges();

        var result = await new ExercisesController(_db).GetStats(ex.Id);

        var stats = Assert.IsType<OkObjectResult>(result.Result).Value as ExerciseStatsDto;
        Assert.Equal("Cable Fly", stats!.ShortName);
    }
}
