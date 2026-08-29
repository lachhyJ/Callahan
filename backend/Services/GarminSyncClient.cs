using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Callahan.Api.Services;

// Thrown when the trigger service isn't configured or can't be reached, or
// when the sync itself reported a failure. Surfaces to the client as 502.
public class GarminSyncUnavailableException(string message) : Exception(message);

// A sync is already in flight in the trigger container. Surfaces as 409.
public class GarminSyncBusyException() : Exception("A Garmin sync is already running.");

// Proxies "Sync Garmin now" to the always-on trigger container
// (scripts/garmin-sync/trigger_server.py), which runs the same Python sync
// the nightly cron does. Returns the trigger's own JSON summary
// ({ ok, wellness, durationMs, error, log[] }) verbatim.
public class GarminSyncClient(HttpClient http, IConfiguration config, ILogger<GarminSyncClient> logger)
{
    public async Task<JsonElement> RunAsync(bool wellness, CancellationToken ct)
    {
        var baseUrl = config["Sync:TriggerBaseUrl"];
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            throw new GarminSyncUnavailableException("Garmin sync isn't set up on this server.");
        }

        var url = $"{baseUrl.TrimEnd('/')}/sync?wellness={(wellness ? "1" : "0")}";
        using var request = new HttpRequestMessage(HttpMethod.Post, url);

        var token = config["Sync:TriggerToken"];
        if (!string.IsNullOrWhiteSpace(token))
        {
            request.Headers.Add("X-Sync-Token", token);
        }

        HttpResponseMessage response;
        try
        {
            response = await http.SendAsync(request, ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            logger.LogWarning(ex, "Garmin sync trigger unreachable at {Url}", url);
            throw new GarminSyncUnavailableException("Couldn't reach the Garmin sync service.");
        }

        if (response.StatusCode == HttpStatusCode.Conflict)
        {
            throw new GarminSyncBusyException();
        }

        JsonElement body;
        try
        {
            body = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
        }
        catch (Exception ex) when (ex is JsonException or HttpRequestException or TaskCanceledException)
        {
            logger.LogWarning(ex, "Garmin sync trigger returned an unreadable response ({Status})", (int)response.StatusCode);
            throw new GarminSyncUnavailableException("The Garmin sync service returned an unexpected response.");
        }

        if (!response.IsSuccessStatusCode)
        {
            var reported = body.ValueKind == JsonValueKind.Object
                && body.TryGetProperty("error", out var e) ? e.GetString() : null;
            throw new GarminSyncUnavailableException(reported ?? "The Garmin sync failed.");
        }

        return body;
    }
}
