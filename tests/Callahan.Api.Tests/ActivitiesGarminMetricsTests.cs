using Callahan.Api.Controllers;
using Callahan.Api.Data;
using Callahan.Api.DTOs;
using Callahan.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Callahan.Api.Tests;

// Garmin's training metrics are parsed out of RawJson on the activity create /
// re-sync path.
public class ActivitiesGarminMetricsTests
{
    private const string Blob = """
        {"activityId":900,"activityTrainingLoad":85.35,"aerobicTrainingEffect":2.2,
         "anaerobicTrainingEffect":2.4,"trainingEffectLabel":"SPEED"}
        """;

    private static AppDbContext NewDb(SqliteConnection conn) => TestData.OpenDb(conn);

    private static CreateActivityRequest Req(string? rawJson, string? garminId = "900") => new(
        Date: new DateOnly(2026, 9, 6),
        Type: "Ultimate",
        DurationSeconds: 2743,
        DistanceKm: 3.5m,
        Calories: null,
        AvgHeartRate: 136,
        Notes: "Ultimate",
        Source: "Garmin",
        GarminActivityId: garminId,
        RawJson: rawJson);

    [Fact]
    public async Task Create_ParsesTrainingMetricsFromRawJson()
    {
        using var conn = new SqliteConnection("DataSource=:memory:");
        using var db = NewDb(conn);
        var controller = new ActivitiesController(db);

        await controller.Create(Req(Blob));

        var saved = await db.Activities.SingleAsync();
        Assert.Equal(85m, saved.ActivityTrainingLoad);
        Assert.Equal(2.2m, saved.AerobicTrainingEffect);
        Assert.Equal(2.4m, saved.AnaerobicTrainingEffect);
        Assert.Equal("SPEED", saved.TrainingEffectLabel);
    }

    [Fact]
    public async Task Create_ReSync_RefreshesMetricsFromTheNewBlob()
    {
        using var conn = new SqliteConnection("DataSource=:memory:");
        using var db = NewDb(conn);
        var controller = new ActivitiesController(db);

        await controller.Create(Req(Blob));
        // Same Garmin id, a blob where the watch no longer reports a load.
        await controller.Create(Req("""{"activityId":900,"averageHR":136.0}"""));

        var saved = await db.Activities.SingleAsync();
        Assert.Null(saved.ActivityTrainingLoad);
        Assert.Null(saved.TrainingEffectLabel);
    }

    // Same-day games synced out of play order (the 13 Nov tournament day:
    // Zephyr synced before Hills) come back newest first by start time, not
    // by Id. A manual entry with no start time sorts after them.
    [Fact]
    public async Task GetAll_OrdersSameDayActivitiesByStartTime_NewestFirst()
    {
        using var conn = new SqliteConnection("DataSource=:memory:");
        using var db = NewDb(conn);
        var controller = new ActivitiesController(db);

        await controller.Create(Req(null, garminId: null) with { Notes = "manual" });
        await controller.Create(Req("""{"startTimeLocal":"2026-09-06 10:44:33"}""", "1") with { Notes = "second" });
        await controller.Create(Req("""{"startTimeLocal":"2026-09-06 09:00:34"}""", "2") with { Notes = "first" });
        await controller.Create(Req("""{"startTimeLocal":"2026-09-06 15:40:59"}""", "3") with { Notes = "third" });
        db.ChangeTracker.Clear();

        var list = (List<ActivityDto>)((OkObjectResult)(await controller.GetAll()).Result!).Value!;

        Assert.Equal(new[] { "third", "second", "first", "manual" }, list.Select(a => a.Notes));
        Assert.Equal(new DateTime(2026, 9, 6, 15, 40, 59), list[0].StartTimeLocal);
        Assert.Null(list[3].StartTimeLocal);
    }
}
