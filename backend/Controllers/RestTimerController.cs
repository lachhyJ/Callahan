using System.Collections.Concurrent;
using System.Text.Json;
using Callahan.Api.Data;
using Callahan.Api.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebPush;
using WebPushSubscription = WebPush.PushSubscription;

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

    private readonly IConfiguration _config;
    private readonly ILogger<RestTimerController> _logger;
    private readonly IServiceScopeFactory _scopeFactory;

    public RestTimerController(IConfiguration config, ILogger<RestTimerController> logger, IServiceScopeFactory scopeFactory)
    {
        _config = config;
        _logger = logger;
        _scopeFactory = scopeFactory;
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
            await Task.Delay(TimeSpan.FromSeconds(durationSeconds), token);
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
            var subscriptions = await db.PushSubscriptions.ToListAsync();

            await SendPushToAll(subscriptions, exerciseName, targetReps, nextSetNumber, totalSets);
        }
        catch (Exception ex)
        {
            // This whole method runs fire-and-forget with nothing else observing it —
            // an uncaught exception here would just vanish with zero trace otherwise.
            _logger.LogError(ex, "Rest timer {TimerId} failed to send push notifications", timerId);
        }
    }

    private async Task SendPushToAll(List<Models.PushSubscription> subscriptions, string exerciseName, string targetReps, int nextSetNumber, int totalSets)
    {
        var publicKey = _config["Vapid:PublicKey"];
        var privateKey = _config["Vapid:PrivateKey"];
        var subject = _config["Vapid:Subject"];

        if (publicKey is null || privateKey is null || subject is null)
        {
            _logger.LogWarning("Vapid config missing — cannot send rest-timer push notifications.");
            return;
        }

        var vapidDetails = new VapidDetails(subject, publicKey, privateKey);
        var client = new WebPushClient();
        // Confirmed on-device: an empty title doesn't collapse to just the OS's
        // "from Callahan" line — iOS fills the blank with "Callahan" anyway, so
        // you get two duplicate mentions instead of one. Real title it is.
        var payload = JsonSerializer.Serialize(new { title = "Rest over", body = $"{targetReps} reps · Set {nextSetNumber}/{totalSets} · {exerciseName}" });

        foreach (var sub in subscriptions)
        {
            try
            {
                var pushSubscription = new WebPushSubscription(sub.Endpoint, sub.P256dh, sub.Auth);
                await client.SendNotificationAsync(pushSubscription, payload, vapidDetails);
            }
            catch (Exception ex)
            {
                // Broad catch deliberately: a bad subscription (expired, unreachable
                // endpoint, malformed keys) must never take down the loop or vanish
                // silently — this runs unattended, seconds after the request ended.
                _logger.LogWarning(ex, "Push failed for subscription {Id}", sub.Id);
            }
        }
    }
}
