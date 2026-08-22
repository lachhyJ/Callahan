using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Callahan.Api.Data;

namespace Callahan.Api.Controllers;

[ApiController]
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
            return Ok(new { status = "ok", database = "connected", version = BuildVersion });
        }
        catch (Exception)
        {
            return StatusCode(503, new { status = "ok", database = "unreachable", version = BuildVersion });
        }
    }
}
