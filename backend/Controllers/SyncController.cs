using System.Text.Json;
using Callahan.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Callahan.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class SyncController(GarminSyncClient garmin) : ControllerBase
{
    // Manual "pull from Garmin now". Proxies to the always-on trigger
    // container; the nightly cron sync is untouched and every write is
    // idempotent, so pressing this any time is safe.
    [HttpPost("garmin")]
    public async Task<ActionResult<JsonElement>> Garmin([FromQuery] bool wellness = false, CancellationToken ct = default)
    {
        try
        {
            return Ok(await garmin.RunAsync(wellness, ct));
        }
        catch (GarminSyncBusyException ex)
        {
            return Conflict(new { error = ex.Message });
        }
        catch (GarminSyncUnavailableException ex)
        {
            return StatusCode(StatusCodes.Status502BadGateway, new { error = ex.Message });
        }
    }
}
