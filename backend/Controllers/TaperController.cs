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
public class TaperController : ControllerBase
{
    private readonly AppDbContext _db;

    public TaperController(AppDbContext db)
    {
        _db = db;
    }

    private static DateOnly MondayOf(DateOnly date)
    {
        var offsetFromMonday = ((int)date.DayOfWeek + 6) % 7; // Mon=0 ... Sun=6
        return date.AddDays(-offsetFromMonday);
    }

    private static TaperEventDto ToDto(TaperEvent e, DateOnly today) =>
        new(e.Id, e.Date, e.Name, e.TaperDays, (e.Date.ToDateTime(TimeOnly.MinValue) - today.ToDateTime(TimeOnly.MinValue)).Days);

    [HttpGet("events")]
    public async Task<ActionResult<List<TaperEventDto>>> GetEvents()
    {
        var today = DateOnly.FromDateTime(DateTime.Now);
        var events = await _db.TaperEvents.OrderByDescending(e => e.Date).ToListAsync();
        return Ok(events.Select(e => ToDto(e, today)).ToList());
    }

    [HttpPost("events")]
    public async Task<ActionResult<TaperEventDto>> CreateEvent(CreateTaperEventRequest request)
    {
        var taperEvent = new TaperEvent
        {
            Date = request.Date,
            Name = request.Name,
            TaperDays = request.TaperDays <= 0 ? 10 : request.TaperDays
        };
        _db.TaperEvents.Add(taperEvent);
        await _db.SaveChangesAsync();

        var today = DateOnly.FromDateTime(DateTime.Now);
        return Ok(ToDto(taperEvent, today));
    }

    [HttpDelete("events/{id}")]
    public async Task<IActionResult> DeleteEvent(int id)
    {
        var taperEvent = await _db.TaperEvents.FindAsync(id);
        if (taperEvent is null) return NotFound();

        _db.TaperEvents.Remove(taperEvent);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpGet("recommendation")]
    public async Task<ActionResult<TaperRecommendationDto>> GetRecommendation()
    {
        var today = DateOnly.FromDateTime(DateTime.Now);

        var upcoming = await _db.TaperEvents
            .Where(e => e.Date >= today)
            .OrderBy(e => e.Date)
            .FirstOrDefaultAsync();

        if (upcoming is null)
        {
            return Ok(new TaperRecommendationDto(null, "none", "No upcoming tournament set.", null, null, null, null, null, null));
        }

        var daysUntil = (upcoming.Date.ToDateTime(TimeOnly.MinValue) - today.ToDateTime(TimeOnly.MinValue)).Days;
        var taperDays = upcoming.TaperDays;

        // Baseline: average weekly gym volume / run distance over the 4 weeks
        // immediately before the taper window opens.
        var taperStart = upcoming.Date.AddDays(-taperDays);
        var baselineStart = taperStart.AddDays(-28);

        var baselineSets = await _db.ExerciseSets
            .Include(s => s.WorkoutSession)
            .Where(s => s.WorkoutSession.Date >= baselineStart && s.WorkoutSession.Date < taperStart)
            .ToListAsync();
        var gymBaselineVolume = baselineSets.Sum(s => s.WeightKg * s.Reps) / 4m;

        var baselineRuns = await _db.Activities
            .Where(a => a.Type == ActivityType.Running && a.Date >= baselineStart && a.Date < taperStart)
            .ToListAsync();
        var runBaselineDistance = baselineRuns.Sum(a => a.DistanceKm ?? 0) / 4m;

        // This week's actuals (Monday-start).
        var weekStart = MondayOf(today);
        var weekEndExclusive = weekStart.AddDays(7);

        var thisWeekSets = await _db.ExerciseSets
            .Include(s => s.WorkoutSession)
            .Where(s => s.WorkoutSession.Date >= weekStart && s.WorkoutSession.Date < weekEndExclusive)
            .ToListAsync();
        var gymThisWeekVolume = thisWeekSets.Sum(s => s.WeightKg * s.Reps);

        var thisWeekRuns = await _db.Activities
            .Where(a => a.Type == ActivityType.Running && a.Date >= weekStart && a.Date < weekEndExclusive)
            .ToListAsync();
        var runThisWeekDistance = thisWeekRuns.Sum(a => a.DistanceKm ?? 0);

        var eventDto = ToDto(upcoming, today);

        if (daysUntil > taperDays)
        {
            return Ok(new TaperRecommendationDto(
                eventDto, "build",
                $"{daysUntil} days until {(upcoming.Name ?? "your tournament")} — normal training, taper guidance kicks in {taperDays} days out.",
                null, null, null, null, null, null));
        }

        string phase;
        decimal targetPct;
        string message;

        if (daysUntil == 0)
        {
            phase = "game_day";
            targetPct = 0m;
            message = $"Game day — {(upcoming.Name ?? "your tournament")} is today. Rest or light activation only.";
        }
        else if (daysUntil <= 2)
        {
            phase = "sharpen";
            targetPct = 0.25m;
            message = $"Sharpen — {daysUntil} day{(daysUntil == 1 ? "" : "s")} out. Keep sessions short and light, aim for around 25% of your usual weekly volume.";
        }
        else if (daysUntil <= taperDays / 2.0)
        {
            phase = "peak_taper";
            targetPct = 0.5m;
            message = $"Peak taper — {daysUntil} days out. Aim for around 50% of your usual weekly volume, hold intensity steady.";
        }
        else
        {
            phase = "early_taper";
            targetPct = 0.75m;
            message = $"Early taper — {daysUntil} days out. Aim for around 75% of your usual weekly volume this week.";
        }

        return Ok(new TaperRecommendationDto(
            eventDto, phase, message,
            targetPct, gymBaselineVolume, gymThisWeekVolume,
            targetPct, runBaselineDistance, runThisWeekDistance));
    }
}
