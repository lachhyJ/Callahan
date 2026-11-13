using Callahan.Api.Controllers;
using Callahan.Api.Data;
using Callahan.Api.DTOs;
using Callahan.Api.Models;
using Callahan.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Callahan.Api.Tests;

// GET /api/activities/{id}/field-timeline returns the lap press windows beside
// the geometry segments, on the same time base, each with its speed-based kind.
public class FieldTimelineLapsTests : IDisposable
{
    private const int GameTypeId = 8;   // seeded "Game"
    private readonly SqliteConnection _conn = new("DataSource=:memory:");
    private readonly AppDbContext _db;
    private readonly int _gameId;

    public FieldTimelineLapsTests()
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

    private async Task<long> PutTrack()
    {
        var req = System.Text.Json.JsonSerializer.Deserialize<UpsertTrackRequest>(
            TestFixtures.LoadTrackPayload(15), new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web))!;
        await new ActivitiesController(_db).ReplaceTrack(_gameId, req);
        _db.ChangeTracker.Clear();
        return (await _db.ActivityTracks.SingleAsync(t => t.ActivityId == _gameId)).StartEpochMs;
    }

    private void AddLaps(long epochMs, params (int StartSec, int DurSec, decimal Avg, decimal Max)[] laps)
    {
        var epoch = DateTimeOffset.FromUnixTimeMilliseconds(epochMs).UtcDateTime;
        int i = 1;
        foreach (var (s, d, avg, max) in laps)
            _db.ActivityLaps.Add(new ActivityLap
            {
                ActivityId = _gameId, LapIndex = i++, StartTimeGmt = epoch.AddSeconds(s),
                DurationSeconds = d, AvgSpeedMps = avg, MaxSpeedMps = max,
            });
        _db.SaveChanges();
        _db.ChangeTracker.Clear();
    }

    private async Task<FieldTimelineDto> Timeline() =>
        (FieldTimelineDto)((OkObjectResult)(await new ActivitiesController(_db).GetFieldTimeline(_gameId)).Result!).Value!;

    [Fact]
    public async Task PressedLaps_AreReturnedRelativeToTrackStart_WithTheirKind()
    {
        var epoch = await PutTrack();
        AddLaps(epoch,
            (0, 1000, 0.5m, 6m),      // > 900 s: warmup
            (1000, 200, 1.4m, 8m),    // point-paced
            (1200, 400, 0.3m, 3m),    // rest
            (1600, 150, 1.2m, 7m));   // point-paced

        var t = await Timeline();

        Assert.Equal(new[] { 1, 2, 3, 4 }, t.Laps.Select(l => l.LapIndex));
        Assert.Equal(new[] { 0, 1000, 1200, 1600 }, t.Laps.Select(l => l.StartSec));
        Assert.Equal(new[] { 1000, 1200, 1600, 1750 }, t.Laps.Select(l => l.EndSec));
        Assert.Equal(new[] { LapKind.Warmup, LapKind.Point, LapKind.Other, LapKind.Point }, t.Laps.Select(l => l.Kind));
    }

    [Fact]
    public async Task TooFewLaps_ToBeASubLog_ReturnNoLaps()
    {
        var epoch = await PutTrack();
        AddLaps(epoch, (0, 3000, 0.6m, 7m), (3000, 2000, 0.6m, 7m));

        Assert.Empty((await Timeline()).Laps);
    }
}
