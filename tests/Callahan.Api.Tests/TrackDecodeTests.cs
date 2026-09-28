using System.Text.Json;
using Callahan.Api.Controllers;
using Callahan.Api.Data;
using Callahan.Api.DTOs;
using Callahan.Api.Models;
using Callahan.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Callahan.Api.Tests;

// The FieldGeometry tests read fixtures with their own parser. Production reads
// the stored track through ActivitiesController's decode, which deliberately
// turns a JsonException into "no track" - so a key rename or casing change in
// the stored format would silently zero every game's on-field numbers while the
// geometry tests stayed green. This sends a real fixture through the real path.
public class TrackDecodeTests
{
    [Fact]
    public async Task AFixtureTrackThroughReplaceTrackIsClassifiedLikeItsBaseline()
    {
        using var conn = new SqliteConnection("DataSource=:memory:");
        conn.Open();
        using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(conn).Options);
        db.Database.EnsureCreated();
        var game = new Activity
        {
            Date = new DateOnly(2026, 4, 11), Type = ActivityType.Ultimate, Source = ActivitySource.Garmin,
            DurationSeconds = 3600, ActivitySessionTypeId = 8, // "Game"
        };
        db.Activities.Add(game);
        db.SaveChanges();

        var request = JsonSerializer.Deserialize<UpsertTrackRequest>(
            TestFixtures.LoadTrackPayload(3), new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        var result = await new ActivitiesController(db).ReplaceTrack(game.Id, request);
        Assert.IsType<OkObjectResult>(result.Result);

        db.ChangeTracker.Clear();
        var saved = db.Activities.Single(a => a.Id == game.Id);
        var baseline = TestFixtures.LoadBaselines().Games.Single(b => b.Game == 3);

        Assert.Equal(LapClassifierMethod.GeometryNoLaps, saved.LapClassifierMethod);
        Assert.InRange(saved.PointsPlayed!.Value, baseline.PointsPlayed - 1, baseline.PointsPlayed + 1);
        var frac = (double)saved.OnFieldSeconds!.Value / (saved.OnFieldSeconds.Value + saved.OffFieldSeconds!.Value);
        Assert.InRange(frac, baseline.OnFieldFraction - 0.03, baseline.OnFieldFraction + 0.03);
    }
}
