using Callahan.Api.Controllers;
using Callahan.Api.Data;
using Callahan.Api.DTOs;
using Callahan.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Callahan.Api.Tests;

// ActivityDto.TrackSampleCount comes from the track's SampleCount column, not
// the 65-100 KB track blob. Every endpoint that returns the DTO has to report
// it: the Garmin sync reads it off the re-sync response to decide whether to
// fetch the GPS stream again, and the game page shows it after a score save.
public class ActivityTrackSampleCountTests : IDisposable
{
    private readonly SqliteConnection _conn = new("DataSource=:memory:");
    private readonly AppDbContext _db;
    private readonly int _activityId;

    public ActivityTrackSampleCountTests()
    {
        _conn.Open();
        _db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_conn).Options);
        _db.Database.EnsureCreated();
        var activity = new Activity
        {
            Date = new DateOnly(2026, 9, 6), Type = ActivityType.Ultimate, Source = ActivitySource.Garmin,
            DurationSeconds = 3600, GarminActivityId = "g-1",
        };
        _db.Activities.Add(activity);
        _db.SaveChanges();
        _db.ActivityTracks.Add(new ActivityTrack { ActivityId = activity.Id, StartEpochMs = 0, SampleCount = 1234, SamplesJson = "{}" });
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

    [Fact]
    public async Task GarminResync_ReportsTheStoredTrack()
    {
        var resync = await new ActivitiesController(_db).Create(new CreateActivityRequest(
            new DateOnly(2026, 9, 6), "Ultimate", 3600, null, null, null, null, "Garmin", "g-1"));
        Assert.Equal(1234, Dto(resync).TrackSampleCount);
    }

    [Fact]
    public async Task ScoreSave_KeepsTheSampleCount()
    {
        var result = await new ActivitiesController(_db).UpdateScore(_activityId, new UpdateActivityScoreRequest(13, 11));
        Assert.Equal(1234, Dto(result).TrackSampleCount);
    }

    [Fact]
    public async Task GetById_ReportsTheSampleCount()
    {
        Assert.Equal(1234, Dto(await new ActivitiesController(_db).GetById(_activityId)).TrackSampleCount);
    }

    [Fact]
    public async Task NoTrack_IsZero()
    {
        var created = await new ActivitiesController(_db).Create(new CreateActivityRequest(
            new DateOnly(2026, 9, 7), "Running", 1800, 5m, null, null, null));
        Assert.Equal(0, Dto(created).TrackSampleCount);
    }
}
