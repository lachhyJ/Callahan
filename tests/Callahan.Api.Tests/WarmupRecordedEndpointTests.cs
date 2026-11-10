using Callahan.Api.Controllers;
using Callahan.Api.Data;
using Callahan.Api.DTOs;
using Callahan.Api.Models;
using Callahan.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Callahan.Api.Tests;

// PUT /api/activities/{id}/warmup stores the flag and reclassifies that one
// game straight away, so the stored numbers and the timeline both follow it.
// Fixture game 15 (Colossus): the inferred start is ~59 min, so "no" moves it
// to 0 and on-field time roughly doubles.
public class WarmupRecordedEndpointTests : IDisposable
{
    private const int GameTypeId = 8;   // seeded "Game"
    private readonly SqliteConnection _conn = new("DataSource=:memory:");
    private readonly AppDbContext _db;
    private readonly int _gameId;

    public WarmupRecordedEndpointTests()
    {
        _conn.Open();
        _db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_conn).Options);
        _db.Database.EnsureCreated();
        var game = new Activity
        {
            Date = new DateOnly(2026, 9, 6), Type = ActivityType.Ultimate, Source = ActivitySource.Garmin,
            DurationSeconds = 6000, ActivitySessionTypeId = GameTypeId,
        };
        _db.Activities.Add(game);
        _db.SaveChanges();
        _gameId = game.Id;
        _db.ChangeTracker.Clear();
    }

    public void Dispose()
    {
        _db.Dispose();
        _conn.Dispose();
        GC.SuppressFinalize(this);
    }

    private static ActivityDto Dto(ActionResult<ActivityDto> r) => (ActivityDto)((OkObjectResult)r.Result!).Value!;

    private async Task PutTrack()
    {
        var req = System.Text.Json.JsonSerializer.Deserialize<UpsertTrackRequest>(
            TestFixtures.LoadTrackPayload(15), new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web))!;
        await new ActivitiesController(_db).ReplaceTrack(_gameId, req);
        _db.ChangeTracker.Clear();
    }

    [Fact]
    public async Task No_ReclassifiesFromSampleZero_AndDontKnowRestoresTheInference()
    {
        await PutTrack();
        var before = Dto(await new ActivitiesController(_db).GetById(_gameId));
        Assert.Null(before.WarmupRecorded);

        _db.ChangeTracker.Clear();
        var no = Dto(await new ActivitiesController(_db).UpdateWarmupRecorded(_gameId, new UpdateWarmupRecordedRequest(false)));
        Assert.False(no.WarmupRecorded);
        Assert.True(no.OnFieldSeconds > before.OnFieldSeconds + 1000, $"{before.OnFieldSeconds} -> {no.OnFieldSeconds}");
        Assert.True(no.PointsPlayed > before.PointsPlayed);
        Assert.Equal(LapFieldClassifier.Version, no.LapClassifierVersion);

        // Persisted, and the timeline agrees: on-field play inside the first hour.
        _db.ChangeTracker.Clear();
        Assert.False((await _db.Activities.SingleAsync(a => a.Id == _gameId)).WarmupRecorded);
        var timeline = (FieldTimelineDto)((OkObjectResult)(await new ActivitiesController(_db).GetFieldTimeline(_gameId)).Result!).Value!;
        Assert.Contains(timeline.Segments, s => s.OnField && s.StartSec < 3000);

        _db.ChangeTracker.Clear();
        var unknown = Dto(await new ActivitiesController(_db).UpdateWarmupRecorded(_gameId, new UpdateWarmupRecordedRequest(null)));
        Assert.Null(unknown.WarmupRecorded);
        Assert.Equal(before.OnFieldSeconds, unknown.OnFieldSeconds);
        Assert.Equal(before.PointsPlayed, unknown.PointsPlayed);
    }

    [Fact]
    public async Task Yes_WithNoLaps_KeepsTheInference()
    {
        await PutTrack();
        var before = Dto(await new ActivitiesController(_db).GetById(_gameId));
        _db.ChangeTracker.Clear();
        var yes = Dto(await new ActivitiesController(_db).UpdateWarmupRecorded(_gameId, new UpdateWarmupRecordedRequest(true)));
        Assert.True(yes.WarmupRecorded);
        Assert.Equal(before.OnFieldSeconds, yes.OnFieldSeconds);
    }

    [Fact]
    public async Task NoTrackYet_StoresTheFlag()
    {
        var r = Dto(await new ActivitiesController(_db).UpdateWarmupRecorded(_gameId, new UpdateWarmupRecordedRequest(false)));
        Assert.False(r.WarmupRecorded);
        Assert.Equal(LapClassifierMethod.NoTrack, r.LapClassifierMethod);
    }

    [Fact]
    public async Task NotUltimate_IsRejected()
    {
        var run = new Activity { Date = new DateOnly(2026, 9, 7), Type = ActivityType.Running, Source = ActivitySource.Garmin, DurationSeconds = 1800 };
        _db.Activities.Add(run);
        await _db.SaveChangesAsync();
        var r = await new ActivitiesController(_db).UpdateWarmupRecorded(run.Id, new UpdateWarmupRecordedRequest(false));
        Assert.IsType<BadRequestObjectResult>(r.Result);
    }

    [Fact]
    public async Task Missing_IsNotFound()
        => Assert.IsType<NotFoundResult>((await new ActivitiesController(_db).UpdateWarmupRecorded(9999, new UpdateWarmupRecordedRequest(false))).Result);
}
