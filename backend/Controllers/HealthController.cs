using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Callahan.Api.Data;

namespace Callahan.Api.Controllers;

[ApiController]
[AllowAnonymous]
[Route("api/[controller]")]
public class HealthController : ControllerBase
{
    private readonly AppDbContext _db;

    // Generated once per process start, not per request — a deploy always
    // starts a fresh container/process, so this doubles as a "has the
    // backend restarted since you loaded the page" signal for the
    // frontend's stale-bundle self-heal, without needing any build-time
    // version stamping.
    private static readonly string BuildVersion = Guid.NewGuid().ToString("N");

    public HealthController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> Get()
    {
        try
        {
            var conn = _db.Database.GetDbConnection();
            await conn.OpenAsync();
            await conn.CloseAsync();
            return Ok(new { status = "ok", version = BuildVersion });
        }
        catch (Exception)
        {
            // Was reporting status "ok" on the failure path, which is what a
            // liveness check reads. Callers get the version either way — the
            // frontend's stale-bundle self-heal depends on it — but the reason
            // for the failure stays off an unauthenticated response.
            return StatusCode(503, new { status = "unavailable", version = BuildVersion });
        }
    }
}
