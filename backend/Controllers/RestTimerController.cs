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
    // In-memory only — acceptable for a single-instance personal app. A pending
    // timer is lost if the container restarts mid-rest, which is rare and low
    // stakes (worst case: one missed alert).
    private static readonly ConcurrentDictionary<string, CancellationTokenSource> PendingTimers = new();

    private readonly ILogger<RestTimerController> _logger;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly double _pushLeadSeconds;

    public RestTimerController(ILogger<RestTimerController> logger, IServiceScopeFactory scopeFactory, IConfiguration config)
    {
        _logger = logger;
        _scopeFactory = scopeFactory;
        _pushLeadSeconds = config.GetValue("RestTimer:PushLeadSeconds", 3.0);
    }

    [HttpPost("schedule")]
    public ActionResult<RestTimerScheduleResponse> Schedule(RestTimerScheduleRequest request)
    {
        var timerId = Guid.NewGuid().ToString("N");
        var cts = new CancellationTokenSource();
        PendingTimers[timerId] = cts;

        _ = FireAfterDelay(timerId, request.DurationSeconds, request.ExerciseName, request.TargetReps, request.NextSetNumber, request.TotalSets, cts.Token);

        return Ok(new RestTimerScheduleResponse(timerId));
    }

    [HttpPost("cancel/{timerId}")]
    public IActionResult Cancel(string timerId)
    {
        if (PendingTimers.TryRemove(timerId, out var cts))
        {
            cts.Cancel();
        }

        return Ok();
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
