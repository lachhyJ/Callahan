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
public class MuscleGroupsController : ControllerBase
{
    private readonly AppDbContext _db;

    public MuscleGroupsController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet("balance")]
    public async Task<ActionResult<List<MuscleBalanceEntryDto>>> GetBalance([FromQuery] DateOnly startDate, [FromQuery] DateOnly endDate)
    {
        var sets = await _db.ExerciseSets
            .Where(s => s.WorkoutSession.Date >= startDate && s.WorkoutSession.Date <= endDate)
            .Include(s => s.Exercise).ThenInclude(e => e.MuscleTargets)
            .ToListAsync();

        var totals = Enum.GetValues<MuscleGroup>().ToDictionary(mg => mg, _ => 0m);

        foreach (var set in sets)
        {
            foreach (var target in set.Exercise.MuscleTargets)
            {
                totals[target.MuscleGroup] += target.IsPrimary ? 1.0m : 0.5m;
            }
        }

        var result = totals
            .Select(kv => new MuscleBalanceEntryDto(kv.Key.ToString(), kv.Value))
            .OrderByDescending(x => x.SetCount)
            .ToList();

        return Ok(result);
    }
}
