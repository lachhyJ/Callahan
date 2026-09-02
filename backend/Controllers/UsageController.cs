using Callahan.Api.Data;
using Callahan.Api.DTOs;
using Callahan.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Callahan.Api.Controllers;

// Collection only. There is deliberately no read endpoint yet: the point of the
// data is to answer navigation questions from unbiased use, and a dashboard
// showing "you never open /history" would change how /history gets opened.
[ApiController]
[Authorize]
[Route("api/[controller]")]
public class UsageController : ControllerBase
{
    // Same trick as HealthController's build version: set once per process, and a
    // deploy always starts a fresh container. Lets each row record how long the
    // backend had been up, which is the marker for "this was probably me
    // checking a change, not using the app".
    private static readonly DateTime ProcessStartedAt = DateTime.UtcNow;

    private const int MaxEventsPerBatch = 50;
    private const int MaxStringLength = 200;

    private readonly AppDbContext _db;
    private readonly ILogger<UsageController> _logger;

    public UsageController(AppDbContext db, ILogger<UsageController> logger)
    {
        _db = db;
        _logger = logger;
    }

    [HttpPost]
    public async Task<IActionResult> Record(UsageBatchRequest request, CancellationToken ct)
    {
        if (request.Events is null || request.Events.Count == 0) return NoContent();

        var now = DateTime.UtcNow;
        var uptimeSeconds = (int)Math.Max(0, (now - ProcessStartedAt).TotalSeconds);

        var rows = request.Events
            .Take(MaxEventsPerBatch)
            .Select(e => new UsageEvent
            {
                // Clamped so a negative or absurd age can't date a row into the
                // future or into last year.
                OccurredAt = now.AddMilliseconds(-Math.Clamp(e.AgeMs, 0, 86_400_000)),
                Kind = e.Kind == "action" ? "action" : "route",
                Path = Truncate(e.Path) ?? "",
                FromPath = Truncate(e.FromPath),
                DwellMs = e.DwellMs is > 0 and < 86_400_000 ? e.DwellMs : null,
                Action = Truncate(e.Action),
                Detail = Truncate(e.Detail),
                BackendUptimeSeconds = uptimeSeconds,
            })
            .ToList();

        try
        {
            _db.UsageEvents.AddRange(rows);
            await _db.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            // Caught so tracking can never break the app it measures — the client
            // ignores the response entirely — but reported as an error and a 500
            // rather than swallowed into a 204. This data is collected over weeks
            // before anyone looks at it, so a persistent write failure that
            // returns success would mean discovering an empty table much later
            // with no way to recover the interval.
            _logger.LogError(ex, "Failed to record {Count} usage events", rows.Count);
            return StatusCode(StatusCodes.Status500InternalServerError);
        }

        return NoContent();
    }

    private static string? Truncate(string? value) =>
        string.IsNullOrEmpty(value) ? null
        : value.Length <= MaxStringLength ? value
        : value[..MaxStringLength];
}
