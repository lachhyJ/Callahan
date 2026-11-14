using Callahan.Api.Controllers;
using Callahan.Api.Data;
using Callahan.Api.DTOs;
using Callahan.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Callahan.Api.Tests;

// Post-game reflections live in their own column because the Garmin re-sync
// owns Notes and rewrites it from the activity title every time.
public class ActivityReflectionTests : IDisposable
{
    private readonly SqliteConnection _conn = new("DataSource=:memory:");
    private readonly AppDbContext _db;
    private readonly int _activityId;

    public ActivityReflectionTests()
    {
        _conn.Open();
        _db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_conn).Options);
        _db.Database.EnsureCreated();
        var activity = new Activity
        {
            Date = new DateOnly(2026, 11, 8), Type = ActivityType.Ultimate, Source = ActivitySource.Garmin,
            DurationSeconds = 3600, GarminActivityId = "g-1", Notes = "vs Manly",
        };
        _db.Activities.Add(activity);
        _db.SaveChanges();
        _db.ChangeTracker.Clear();
        _activityId = activity.Id;
    }

    public void Dispose()
    {
        _db.Dispose();
        _conn.Dispose();
        GC.SuppressFinalize(this);
    }

    private static ActivityDto Dto(ActionResult<ActivityDto> r) => (ActivityDto)((OkObjectResult)r.Result!).Value!;

    private Task<ActionResult<ActivityDto>> Save(string? text) =>
        new ActivitiesController(_db).UpdateReflection(_activityId, new UpdateActivityReflectionRequest(text));

    [Fact]
    public async Task Save_TrimsAndIsReturnedByGetById()
    {
        Assert.Equal("Cut harder on the dump.", Dto(await Save("  Cut harder on the dump.\n")).Reflection);
        _db.ChangeTracker.Clear();
        Assert.Equal("Cut harder on the dump.", Dto(await new ActivitiesController(_db).GetById(_activityId)).Reflection);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   \n ")]
    public async Task Blank_ClearsIt(string? blank)
    {
        await Save("something");
        Assert.Null(Dto(await Save(blank)).Reflection);
    }

    [Fact]
    public async Task TooLong_IsRejected()
    {
        var result = await Save(new string('x', ActivitiesController.MaxReflectionLength + 1));
        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task UnknownActivity_IsNotFound()
    {
        var result = await new ActivitiesController(_db).UpdateReflection(9999, new UpdateActivityReflectionRequest("x"));
        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task GarminResync_LeavesItAlone()
    {
        await Save("Two back-to-back to open.");
        _db.ChangeTracker.Clear();
        var resync = await new ActivitiesController(_db).Create(new CreateActivityRequest(
            new DateOnly(2026, 11, 8), "Ultimate", 3600, null, null, null, "vs Manly (renamed)", "Garmin", "g-1"));
        Assert.Equal("vs Manly (renamed)", Dto(resync).Notes);
        Assert.Equal("Two back-to-back to open.", Dto(resync).Reflection);
    }
}
