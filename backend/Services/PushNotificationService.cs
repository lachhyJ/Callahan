using System.Net;
using System.Text.Json;
using Callahan.Api.Data;
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
    private readonly IHttpClientFactory? _httpClientFactory;
    private readonly AppDbContext? _db;

    public PushNotificationService(IConfiguration config, ILogger<PushNotificationService> logger,
        IHttpClientFactory? httpClientFactory = null, AppDbContext? db = null)
    {
        _config = config;
        _logger = logger;
        _httpClientFactory = httpClientFactory;
        _db = db;
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
        // A factory client rather than WebPushClient's own: that one news up an
        // HttpClient per instance, and nothing ever disposed it.
        var client = _httpClientFactory is null ? new WebPushClient() : new WebPushClient(_httpClientFactory.CreateClient("webpush"));
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

        var expired = new List<Models.PushSubscription>();
        foreach (var sub in subscriptions)
        {
            try
            {
                var pushSubscription = new WebPushSubscription(sub.Endpoint, sub.P256dh, sub.Auth);
                await client.SendNotificationAsync(pushSubscription, payload, options);
            }
            catch (WebPushException ex) when (ex.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Gone)
            {
                // The push service says this subscription no longer exists (the
                // browser unsubscribed, or the PWA was removed). Without pruning,
                // every later push retried it forever.
                _logger.LogInformation("Push subscription {Id} has expired; removing it", sub.Id);
                expired.Add(sub);
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

        if (expired.Count > 0 && _db is not null)
        {
            _db.PushSubscriptions.RemoveRange(expired);
            await _db.SaveChangesAsync();
        }
    }
}
