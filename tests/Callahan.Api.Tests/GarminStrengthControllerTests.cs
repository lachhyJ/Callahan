using Callahan.Api.Controllers;
using Callahan.Api.Data;
using Callahan.Api.DTOs;
using Callahan.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Callahan.Api.Tests;

// A Garmin strength-training activity is matched to an existing WorkoutSession
// by same-day + time-window overlap, never creates its own record, and falls
// back to a pending-review row when it can't be matched to exactly one session.
public class GarminStrengthControllerTests
{
    private static AppDbContext NewDb(SqliteConnection conn)
    {
        conn.Open();
        var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(conn).Options);
        db.Database.EnsureCreated();
        return db;
    }

    private static WorkoutSession Session(DateOnly date, DateTime? startedAt) => new()
    {
        Date = date,
        StartedAt = startedAt,
        FinishedAt = startedAt?.AddHours(1),
    };

    private static SyncGarminStrengthRequest Req(
        string garminId, DateOnly date, DateTime? startedAt, int duration = 3600) => new(
        GarminActivityId: garminId,
        Date: date,
        StartedAt: startedAt,
        DurationSeconds: duration,
        Calories: 350,
        AvgHeartRate: 120,
        ActivityTrainingLoad: 60m,
        AerobicTrainingEffect: 1.5m,
        AnaerobicTrainingEffect: 0.5m,
        TrainingEffectLabel: "RECOVERY",
        Notes: "Strength",
        RawJson: """{"activityId":"g1"}""");

    [Fact]
    public async Task Sync_SingleSameDaySessionInWindow_AutoMatchesAndEnrichesIt()
    {
        using var conn = new SqliteConnection("DataSource=:memory:");
        using var db = NewDb(conn);
        var date = new DateOnly(2026, 9, 10);
        var session = Session(date, new DateTime(2026, 9, 10, 18, 0, 0));
        db.WorkoutSessions.Add(session);
        await db.SaveChangesAsync();

        var controller = new GarminStrengthController(db);
        var result = Assert.IsType<GarminStrengthSyncResultDto>(
            Assert.IsType<OkObjectResult>((await controller.Sync(
                Req("g1", date, new DateTime(2026, 9, 10, 18, 5, 0)))).Result).Value);

        Assert.True(result.Matched);
        Assert.Equal(session.Id, result.WorkoutSessionId);
        Assert.Null(result.PendingId);

        var saved = await db.WorkoutSessions.SingleAsync();
        Assert.Equal("g1", saved.GarminActivityId);
        Assert.Equal(350, saved.GarminCalories);
        Assert.Equal(60m, saved.GarminActivityTrainingLoad);
    }

    [Fact]
    public async Task Sync_NoSessionThatDay_CreatesPendingRow()
    {
        using var conn = new SqliteConnection("DataSource=:memory:");
        using var db = NewDb(conn);
        var controller = new GarminStrengthController(db);

        var date = new DateOnly(2026, 9, 10);
        var result = Assert.IsType<GarminStrengthSyncResultDto>(
            Assert.IsType<OkObjectResult>((await controller.Sync(Req("g1", date, null))).Result).Value);

        Assert.False(result.Matched);
        Assert.Null(result.WorkoutSessionId);
        Assert.NotNull(result.PendingId);
        Assert.Single(await db.PendingGarminStrengthActivities.ToListAsync());
    }

    [Fact]
    public async Task Sync_MultipleOverlappingSessions_FallsBackToPendingRatherThanGuessing()
    {
        using var conn = new SqliteConnection("DataSource=:memory:");
        using var db = NewDb(conn);
        var date = new DateOnly(2026, 9, 10);
        db.WorkoutSessions.Add(Session(date, new DateTime(2026, 9, 10, 18, 0, 0)));
        db.WorkoutSessions.Add(Session(date, new DateTime(2026, 9, 10, 18, 10, 0)));
        await db.SaveChangesAsync();

        var controller = new GarminStrengthController(db);
        var result = Assert.IsType<GarminStrengthSyncResultDto>(
            Assert.IsType<OkObjectResult>((await controller.Sync(
                Req("g1", date, new DateTime(2026, 9, 10, 18, 5, 0)))).Result).Value);

        Assert.False(result.Matched);
        Assert.All(await db.WorkoutSessions.ToListAsync(), s => Assert.Null(s.GarminActivityId));
    }

    [Fact]
    public async Task Sync_SessionOutsideToleranceWindow_DoesNotMatch()
    {
        using var conn = new SqliteConnection("DataSource=:memory:");
        using var db = NewDb(conn);
        var date = new DateOnly(2026, 9, 10);
        db.WorkoutSessions.Add(Session(date, new DateTime(2026, 9, 10, 6, 0, 0))); // morning session
        await db.SaveChangesAsync();

        var controller = new GarminStrengthController(db);
        var result = Assert.IsType<GarminStrengthSyncResultDto>(
            Assert.IsType<OkObjectResult>((await controller.Sync(
                Req("g1", date, new DateTime(2026, 9, 10, 18, 0, 0)))).Result).Value);

        // Zero candidates within the window -> pending, not a wrong auto-match.
        Assert.False(result.Matched);
    }

    [Fact]
    public async Task Sync_ReSyncOfAlreadyMatchedActivity_RefreshesInPlaceWithoutDuplicating()
    {
        using var conn = new SqliteConnection("DataSource=:memory:");
        using var db = NewDb(conn);
        var date = new DateOnly(2026, 9, 10);
        db.WorkoutSessions.Add(Session(date, new DateTime(2026, 9, 10, 18, 0, 0)));
        await db.SaveChangesAsync();

        var controller = new GarminStrengthController(db);
        await controller.Sync(Req("g1", date, new DateTime(2026, 9, 10, 18, 0, 0)));
        await controller.Sync(Req("g1", date, new DateTime(2026, 9, 10, 18, 0, 0), duration: 4000));

        Assert.Single(await db.WorkoutSessions.ToListAsync());
        var saved = await db.WorkoutSessions.SingleAsync();
        Assert.Equal(4000, saved.GarminDurationSeconds);
    }

    [Fact]
    public async Task Link_ManuallyResolvesAPendingActivityAndRemovesItFromTheQueue()
    {
        using var conn = new SqliteConnection("DataSource=:memory:");
        using var db = NewDb(conn);
        var date = new DateOnly(2026, 9, 10);
        var chosen = Session(date, new DateTime(2026, 9, 10, 18, 0, 0));
        var other = Session(date, new DateTime(2026, 9, 10, 18, 5, 0));
        db.WorkoutSessions.AddRange(chosen, other);
        await db.SaveChangesAsync();

        var controller = new GarminStrengthController(db);
        var syncResult = Assert.IsType<GarminStrengthSyncResultDto>(
            Assert.IsType<OkObjectResult>((await controller.Sync(
                Req("g1", date, new DateTime(2026, 9, 10, 18, 2, 0)))).Result).Value);

        // Two overlapping candidates - can't auto-match, so it's pending.
        Assert.False(syncResult.Matched);
        Assert.NotNull(syncResult.PendingId);

        await controller.Link(syncResult.PendingId!.Value, chosen.Id);

        Assert.Empty(await db.PendingGarminStrengthActivities.ToListAsync());
        var savedChosen = await db.WorkoutSessions.SingleAsync(s => s.Id == chosen.Id);
        Assert.Equal("g1", savedChosen.GarminActivityId);
        var savedOther = await db.WorkoutSessions.SingleAsync(s => s.Id == other.Id);
        Assert.Null(savedOther.GarminActivityId);
    }

    [Fact]
    public async Task Dismiss_RemovesThePendingRowWithoutTouchingAnySession()
    {
        using var conn = new SqliteConnection("DataSource=:memory:");
        using var db = NewDb(conn);
        var controller = new GarminStrengthController(db);

        var syncResult = Assert.IsType<GarminStrengthSyncResultDto>(
            Assert.IsType<OkObjectResult>((await controller.Sync(
                Req("g1", new DateOnly(2026, 9, 10), null))).Result).Value);

        await controller.Dismiss(syncResult.PendingId!.Value);

        Assert.Empty(await db.PendingGarminStrengthActivities.ToListAsync());
    }
}
