using Callahan.Api.Controllers;
using Callahan.Api.Data;
using Callahan.Api.DTOs;
using Callahan.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Callahan.Api.Tests;

// Garmin's training metrics are parsed out of RawJson on the activity create /
// re-sync path, and backfillable in bulk for rows synced before the columns
// existed.
public class ActivitiesGarminMetricsTests
{
    private const string Blob = """
        {"activityId":900,"activityTrainingLoad":85.35,"aerobicTrainingEffect":2.2,
         "anaerobicTrainingEffect":2.4,"trainingEffectLabel":"SPEED"}
        """;

    private static AppDbContext NewDb(SqliteConnection conn)
    {
        conn.Open();
        var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(conn).Options);
        db.Database.EnsureCreated();
        return db;
    }

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

    [Fact]
    public async Task Backfill_PopulatesRowsSyncedBeforeTheColumnsExisted_AndIsIdempotent()
    {
        using var conn = new SqliteConnection("DataSource=:memory:");
        using var db = NewDb(conn);

        // Simulate a pre-existing row: RawJson present, metric columns still null.
        db.Activities.Add(new Activity
        {
            Date = new DateOnly(2026, 9, 6),
            Type = ActivityType.Ultimate,
            Source = ActivitySource.Garmin,
            DurationSeconds = 2743,
            RawJson = Blob,
        });
        db.Activities.Add(new Activity
        {
            Date = new DateOnly(2026, 9, 7),
            Type = ActivityType.Ultimate,
            Source = ActivitySource.Manual,
            DurationSeconds = 1800,
            RawJson = null, // manual entry, nothing to parse
        });
        await db.SaveChangesAsync();

        var controller = new ActivitiesController(db);

        var first = Assert.IsType<BackfillGarminMetricsResponse>(
            Assert.IsType<OkObjectResult>((await controller.BackfillGarminMetrics()).Result).Value);
        Assert.Equal(1, first.Scanned);   // the null-RawJson row is not scanned
        Assert.Equal(1, first.Updated);

        var saved = await db.Activities.SingleAsync(a => a.RawJson != null);
        Assert.Equal(85m, saved.ActivityTrainingLoad);

        var second = Assert.IsType<BackfillGarminMetricsResponse>(
            Assert.IsType<OkObjectResult>((await controller.BackfillGarminMetrics()).Result).Value);
        Assert.Equal(1, second.Scanned);
        Assert.Equal(0, second.Updated);   // nothing changes on a re-run
    }
}
