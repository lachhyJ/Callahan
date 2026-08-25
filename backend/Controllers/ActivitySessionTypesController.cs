using Callahan.Api.Data;
using Callahan.Api.DTOs;
using Callahan.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Callahan.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class ActivitySessionTypesController : ControllerBase
{
    private readonly AppDbContext _db;

    public ActivitySessionTypesController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<ActionResult<List<ActivitySessionTypeDto>>> GetAll(string? type = null)
    {
        var query = _db.ActivitySessionTypes.AsQueryable();
        if (type is not null)
        {
            if (!Enum.TryParse<ActivityType>(type, ignoreCase: true, out var activityType))
            {
                return BadRequest(new { error = $"Unknown activity type '{type}'." });
            }
            query = query.Where(t => t.ActivityType == activityType);
        }

        var types = await query
            .OrderBy(t => t.SortOrder)
            .Select(t => new ActivitySessionTypeDto(t.Id, t.Name, t.ActivityType.ToString()))
            .ToListAsync();

        return Ok(types);
    }
}
