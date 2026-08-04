using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RepLog.Api.Data;

namespace RepLog.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class HealthController : ControllerBase
{
    private readonly AppDbContext _db;

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
            return Ok(new { status = "ok", database = "connected" });
        }
        catch (Exception)
        {
            return StatusCode(503, new { status = "ok", database = "unreachable" });
        }
    }
}
