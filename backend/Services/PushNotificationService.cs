using System.Text.Json;
using Callahan.Api.Models;
using WebPush;
using WebPushSubscription = WebPush.PushSubscription;

namespace Callahan.Api.Services;

// Extracted from RestTimerController so a second feature (TaperReminderService)
// doesn't have to duplicate the VAPID/WebPush plumbing.
public class PushNotificationService
{
    private readonly IConfiguration _config;
    private readonly ILogger<PushNotificationService> _logger;

    public PushNotificationService(IConfiguration config, ILogger<PushNotificationService> logger)
    {
        _config = config;
        _logger = logger;
    }

    public async Task SendToAllAsync(List<Models.PushSubscription> subscriptions, string title, string body)
    {
        var publicKey = _config["Vapid:PublicKey"];
        var privateKey = _config["Vapid:PrivateKey"];
        var subject = _config["Vapid:Subject"];

        if (publicKey is null || privateKey is null || subject is null)
        {
            // Include the title (e.g. "Rest is over" vs "Taper check-in") since
            // this is shared by multiple callers — a bare "cannot send push
            // notifications" gives no way to tell which feature just no-opped.
            _logger.LogWarning("Vapid config missing — cannot send push notification {Title}", title);
            return;
        }

        var vapidDetails = new VapidDetails(subject, publicKey, privateKey);
        var client = new WebPushClient();
        // Confirmed on-device: an empty title doesn't collapse to just the OS's
        // "from Callahan" line — iOS fills the blank with "Callahan" anyway, so
        // you get two duplicate mentions instead of one. Real title it is.
        var payload = JsonSerializer.Serialize(new { title, body });
        // Without an explicit Urgency, Apple's web push gateway can defer delivery
        // (worse under Low Power Mode) — observed as late/inconsistent rest-timer
        // alerts specifically while backgrounded in another app. "high" is the
        // signal for time-sensitive delivery per RFC 8030.
        var options = new Dictionary<string, object>
        {
            ["vapidDetails"] = vapidDetails,
            ["headers"] = new Dictionary<string, object> { ["Urgency"] = "high" },
        };

        foreach (var sub in subscriptions)
        {
            try
            {
                var pushSubscription = new WebPushSubscription(sub.Endpoint, sub.P256dh, sub.Auth);
                await client.SendNotificationAsync(pushSubscription, payload, options);
            }
            catch (Exception ex)
            {
                // Broad catch deliberately: a bad subscription (expired, unreachable
                // endpoint, malformed keys) must never take down the loop or vanish
                // silently — this runs unattended, seconds (or a poll cycle) after
                // whatever triggered it.
                _logger.LogWarning(ex, "Push failed for subscription {Id}", sub.Id);
            }
        }
    }
}
