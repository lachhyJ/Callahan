using Callahan.Api.Data;
using Callahan.Api.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Callahan.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class TrendsController : ControllerBase
{
    private readonly AppDbContext _db;

    public TrendsController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<ActionResult<List<TrendPointDto>>> GetTrends([FromQuery] int months = 6)
    {
        var today = DateOnly.FromDateTime(DateTime.Now);
        var currentMonthStart = new DateOnly(today.Year, today.Month, 1);
        var earliestMonthStart = currentMonthStart.AddMonths(-(months - 1));

        var sets = await _db.ExerciseSets
            .Where(s => s.WorkoutSession.Date >= earliestMonthStart)
            .Include(s => s.WorkoutSession)
            .ToListAsync();

        var workoutDates = await _db.WorkoutSessions
            .Where(s => s.Date >= earliestMonthStart)
            .Select(s => s.Date)
            .ToListAsync();

        var runDates = await _db.RunningSessions
            .Where(s => s.Date >= earliestMonthStart)
            .Select(s => s.Date)
            .ToListAsync();

        var volumeByMonth = new Dictionary<DateOnly, decimal>();
        var gymByMonth = new Dictionary<DateOnly, int>();
        var runByMonth = new Dictionary<DateOnly, int>();
        for (var i = 0; i < months; i++)
        {
            var monthStart = earliestMonthStart.AddMonths(i);
            volumeByMonth[monthStart] = 0;
            gymByMonth[monthStart] = 0;
            runByMonth[monthStart] = 0;
        }

        foreach (var s in sets)
        {
            var monthStart = new DateOnly(s.WorkoutSession.Date.Year, s.WorkoutSession.Date.Month, 1);
            volumeByMonth[monthStart] += s.WeightKg * s.Reps;
        }
        foreach (var d in workoutDates)
        {
            gymByMonth[new DateOnly(d.Year, d.Month, 1)]++;
        }
        foreach (var d in runDates)
        {
            runByMonth[new DateOnly(d.Year, d.Month, 1)]++;
        }

        var result = volumeByMonth.Keys
            .OrderBy(m => m)
            .Select(m => new TrendPointDto(m, volumeByMonth[m], gymByMonth[m], runByMonth[m]))
            .ToList();

        return Ok(result);
    }
}
