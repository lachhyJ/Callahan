using System.Collections.Concurrent;
using Callahan.Api.Data;
using Callahan.Api.DTOs;
using Callahan.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Callahan.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class RestTimerController : ControllerBase
{
    private sealed record PendingTimer(
        CancellationTokenSource Cts,
        DateTimeOffset ScheduledAtUtc,
        DateTimeOffset EndsAtUtc,
        string ExerciseName,
        string TargetReps,
        int NextSetNumber,
        int TotalSets);

    // In-memory only — acceptable for a single-instance personal app. A pending
    // timer is lost if the container restarts mid-rest, which is rare and low
    // stakes (worst case: one missed alert).
    private static readonly ConcurrentDictionary<string, PendingTimer> PendingTimers = new();

    private readonly ILogger<RestTimerController> _logger;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly double _pushLeadSeconds;

    public RestTimerController(ILogger<RestTimerController> logger, IServiceScopeFactory scopeFactory, IConfiguration config)
    {
        _logger = logger;
        _scopeFactory = scopeFactory;
        _pushLeadSeconds = config.GetValue("RestTimer:PushLeadSeconds", 3.0);
    }

    // A rest timer is minutes, not hours. Unbounded, a negative duration made
    // Math.Clamp below throw (max < min) inside a fire-and-forget task where
    // nothing observes it, and a huge one parked a CancellationTokenSource in
    // the static dictionary indefinitely.
    private const int MinDurationSeconds = 5;
    private const int MaxDurationSeconds = 3600;

    [HttpPost("schedule")]
    public ActionResult<RestTimerScheduleResponse> Schedule(RestTimerScheduleRequest request)
    {
        if (request.DurationSeconds < MinDurationSeconds || request.DurationSeconds > MaxDurationSeconds)
        {
            return BadRequest(new { error = $"DurationSeconds must be between {MinDurationSeconds} and {MaxDurationSeconds}." });
        }

        var timerId = Guid.NewGuid().ToString("N");
        var cts = new CancellationTokenSource();
        var scheduledAtUtc = DateTimeOffset.UtcNow;
        var endsAtUtc = scheduledAtUtc.AddSeconds(request.DurationSeconds);
        PendingTimers[timerId] = new PendingTimer(cts, scheduledAtUtc, endsAtUtc, request.ExerciseName, request.TargetReps, request.NextSetNumber, request.TotalSets);

        _ = FireAfterDelay(timerId, request.DurationSeconds, request.ExerciseName, request.TargetReps, request.NextSetNumber, request.TotalSets, cts.Token);

        return Ok(new RestTimerScheduleResponse(timerId));
    }

    [HttpPost("cancel/{timerId}")]
    public IActionResult Cancel(string timerId)
    {
        if (PendingTimers.TryRemove(timerId, out var timer))
        {
            timer.Cts.Cancel();
        }

        return Ok();
    }

    // Returns the most-recently-scheduled pending timer, for the Garmin data field
    // to poll. There should only ever be one entry (the phone cancels before it
    // reschedules), but that invariant isn't enforced anywhere, so order by
    // ScheduledAtUtc — not EndsAtUtc, which isn't a proxy for scheduling order —
    // rather than assume it.
    //
    // A timer disappears from PendingTimers ~PushLeadSeconds before it actually
    // ends (see FireAfterDelay's finally). A 204 here means "no timer scheduled",
    // not "the last-seen timer finished" — callers that have already seen a
    // TimerId should keep counting down locally rather than treat a later 204 as
    // a cancellation.
    [HttpGet("current")]
    public ActionResult<RestTimerCurrentResponse> Current()
    {
        var newest = PendingTimers
            .OrderByDescending(kvp => kvp.Value.ScheduledAtUtc)
            .Select(kvp => (TimerId: kvp.Key, Timer: kvp.Value))
            .FirstOrDefault();

        if (newest.Timer is null)
        {
            return NoContent();
        }

        return Ok(new RestTimerCurrentResponse(
            newest.TimerId,
            newest.Timer.EndsAtUtc,
            newest.Timer.ExerciseName,
            newest.Timer.TargetReps,
            newest.Timer.NextSetNumber,
            newest.Timer.TotalSets));
    }

    private async Task FireAfterDelay(string timerId, int durationSeconds, string exerciseName, string targetReps, int nextSetNumber, int totalSets, CancellationToken token)
    {
        try
        {
            // Fire the send early enough that the server → push service → APNs →
            // device hop lands close to when the clock actually hits zero, rather
            // than starting that hop only after the rest period has already ended.
            var leadIn = Math.Clamp(_pushLeadSeconds, 0, durationSeconds);
            await Task.Delay(TimeSpan.FromSeconds(durationSeconds - leadIn), token);
        }
        catch (TaskCanceledException)
        {
            return;
        }
        finally
        {
            PendingTimers.TryRemove(timerId, out _);
        }

        try
        {
            // Use a fresh scope — the request that started this delay has long since
            // ended, so HttpContext/its RequestServices are no longer valid here.
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var pushService = scope.ServiceProvider.GetRequiredService<PushNotificationService>();
            var subscriptions = await db.PushSubscriptions.ToListAsync();

            await pushService.SendToAllAsync(subscriptions, "Rest over", $"{targetReps} reps · Set {nextSetNumber}/{totalSets} · {exerciseName}");
        }
        catch (Exception ex)
        {
            // This whole method runs fire-and-forget with nothing else observing it —
            // an uncaught exception here would just vanish with zero trace otherwise.
            _logger.LogError(ex, "Rest timer {TimerId} failed to send push notifications", timerId);
        }
    }
}
