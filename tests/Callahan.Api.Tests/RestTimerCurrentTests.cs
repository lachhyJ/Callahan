using System.Collections;
using System.Reflection;
using Callahan.Api.Controllers;
using Callahan.Api.Data;
using Callahan.Api.DTOs;
using Callahan.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Callahan.Api.Tests;

// GET /api/resttimer/current is what the Garmin Connect IQ data field polls.
// The behaviour that matters: it exposes an absolute EndsAtUtc (so poll
// latency doesn't delay the buzz), returns 204 when nothing is pending, and
// returns the newest timer by scheduling order (not end time) if more than
// one is somehow in flight.
public class RestTimerCurrentTests
{
    // PendingTimers is a private static field on the controller (in-memory
    // state shared across the whole process, by design — see the controller's
    // own comment). Tests run against the same static field, so each test
    // must clear it first or it leaks entries from whichever test ran before it.
    private static void ClearPendingTimers()
    {
        var field = typeof(RestTimerController).GetField("PendingTimers", BindingFlags.NonPublic | BindingFlags.Static)!;
        ((IDictionary)field.GetValue(null)!).Clear();
    }

    private static (RestTimerController Controller, ServiceProvider Services) NewController()
    {
        ClearPendingTimers();

        var services = new ServiceCollection();
        services.AddDbContext<AppDbContext>(o => o.UseSqlite(new SqliteConnection("DataSource=:memory:")));
        services.AddSingleton<PushNotificationService>(_ =>
            new PushNotificationService(new ConfigurationBuilder().Build(), NullLogger<PushNotificationService>.Instance));
        var provider = services.BuildServiceProvider();

        using (var scope = provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Database.OpenConnection();
            db.Database.EnsureCreated();
        }

        var config = new ConfigurationBuilder().Build();
        var controller = new RestTimerController(NullLogger<RestTimerController>.Instance, provider.GetRequiredService<IServiceScopeFactory>(), config);
        return (controller, provider);
    }

    private static RestTimerScheduleRequest Request(int durationSeconds, string exercise = "Back Squat") =>
        new(durationSeconds, exercise, "5", 2, 4);

    [Fact]
    public void ReturnsNoContentWhenNothingIsPending()
    {
        var (controller, _) = NewController();

        var result = controller.Current();

        Assert.IsType<NoContentResult>(result.Result);
    }

    [Fact]
    public void ReturnsTheScheduledTimerWithEndsAtUtcWithinTolerance()
    {
        var (controller, _) = NewController();
        var before = DateTimeOffset.UtcNow;

        controller.Schedule(Request(90));
        var result = controller.Current();

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var body = Assert.IsType<RestTimerCurrentResponse>(ok.Value);
        Assert.InRange(body.EndsAtUtc, before.AddSeconds(90), before.AddSeconds(92));
        Assert.Equal("Back Squat", body.ExerciseName);
        Assert.Equal("5", body.TargetReps);
        Assert.Equal(2, body.NextSetNumber);
        Assert.Equal(4, body.TotalSets);
    }

    [Fact]
    public void ReturnsNoContentAfterCancel()
    {
        var (controller, _) = NewController();

        var scheduled = controller.Schedule(Request(90));
        var ok = Assert.IsType<OkObjectResult>(scheduled.Result);
        var timerId = Assert.IsType<RestTimerScheduleResponse>(ok.Value).TimerId;

        controller.Cancel(timerId);
        var result = controller.Current();

        Assert.IsType<NoContentResult>(result.Result);
    }

    [Fact]
    public void ReturnsTheNewestWhenTwoAreScheduledBackToBack()
    {
        var (controller, _) = NewController();

        // A 60s timer scheduled after a 180s one ends first — EndsAtUtc must
        // not be used as the ordering key, only ScheduledAtUtc.
        controller.Schedule(Request(180, "Deadlift"));
        controller.Schedule(Request(60, "Bench Press"));

        var result = controller.Current();

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var body = Assert.IsType<RestTimerCurrentResponse>(ok.Value);
        Assert.Equal("Bench Press", body.ExerciseName);
    }

    [Fact]
    public void EndsAtUtcReflectsTheFullDurationNotDurationMinusLeadIn()
    {
        var (controller, _) = NewController();
        var before = DateTimeOffset.UtcNow;

        // Default PushLeadSeconds is 3 — if EndsAtUtc were computed as
        // duration - leadIn, this would land ~3s short of +90s.
        controller.Schedule(Request(90));
        var result = controller.Current();

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var body = Assert.IsType<RestTimerCurrentResponse>(ok.Value);
        Assert.True(body.EndsAtUtc >= before.AddSeconds(90));
    }

    [Fact]
    public void ServerNowUtcIsTheCurrentTimeNotTheScheduleTime()
    {
        var (controller, _) = NewController();

        controller.Schedule(Request(90));
        var before = DateTimeOffset.UtcNow;
        var result = controller.Current();
        var after = DateTimeOffset.UtcNow;

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var body = Assert.IsType<RestTimerCurrentResponse>(ok.Value);
        Assert.InRange(body.ServerNowUtc, before, after);
    }
}
