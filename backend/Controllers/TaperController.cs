using Callahan.Api.Data;
using Callahan.Api.DTOs;
using Callahan.Api.Models;
using Callahan.Api.Services;
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
    private readonly TaperConsultService _consultService;

    public TaperController(AppDbContext db, TaperConsultService consultService)
    {
        _db = db;
        _consultService = consultService;
    }

    private static DateOnly MondayOf(DateOnly date)
    {
        var offsetFromMonday = ((int)date.DayOfWeek + 6) % 7; // Mon=0 ... Sun=6
        return date.AddDays(-offsetFromMonday);
    }

    private static TaperEventDto ToDto(TaperEvent e, DateOnly today) =>
        new(e.Id, e.Date, e.Name, e.TaperDays, (e.Date.ToDateTime(TimeOnly.MinValue) - today.ToDateTime(TimeOnly.MinValue)).Days, e.PlannedReductionPercent);

    private static TaperCheckInDto ToCheckInDto(TaperCheckIn c, DateOnly eventDate) =>
        new(c.Id, c.Date, c.Energy, c.Soreness, c.Motivation, c.Context, c.Date > eventDate);

    [HttpGet("events")]
    public async Task<ActionResult<List<TaperEventDto>>> GetEvents([FromQuery] DateOnly? from = null, [FromQuery] DateOnly? to = null)
    {
        var today = DateOnly.FromDateTime(DateTime.Now);
        var query = _db.TaperEvents.AsQueryable();
        if (from is not null) query = query.Where(e => e.Date >= from);
        if (to is not null) query = query.Where(e => e.Date <= to);
        var events = await query.OrderByDescending(e => e.Date).ToListAsync();
        return Ok(events.Select(e => ToDto(e, today)).ToList());
    }

    [HttpPost("events")]
    public async Task<ActionResult<TaperEventDto>> CreateEvent(CreateTaperEventRequest request)
    {
        var taperDays = request.TaperDays <= 0 ? 10 : request.TaperDays;
        var taperEvent = new TaperEvent
        {
            Date = request.Date,
            Name = request.Name,
            TaperDays = taperDays,
            // Deepest planned cut (the "sharpen" phase, always reached before
            // game day regardless of taper length) expressed as a reduction —
            // 1 - TargetPct. Fixed at creation so a finished taper keeps a
            // stable planned figure to compare actuals against later, rather
            // than one that would keep changing with "days until" if
            // recomputed live.
            PlannedReductionPercent = 1m - (TaperPhaseCalculator.Compute(2, taperDays, request.Name).TargetPct ?? 0.25m)
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

        var tapersCompleted = await _db.TaperEvents.CountAsync(e => e.Date < today);

        if (upcoming is null)
        {
            return Ok(new TaperRecommendationDto(null, "none", "No upcoming tournament set.", null, null, null, null, null, null, tapersCompleted));
        }

        var daysUntil = (upcoming.Date.ToDateTime(TimeOnly.MinValue) - today.ToDateTime(TimeOnly.MinValue)).Days;
        var taperDays = upcoming.TaperDays;
        var phase = TaperPhaseCalculator.Compute(daysUntil, taperDays, upcoming.Name);
        var eventDto = ToDto(upcoming, today);

        if (phase.Phase == "build")
        {
            return Ok(new TaperRecommendationDto(eventDto, phase.Phase, phase.Message, null, null, null, null, null, null, tapersCompleted));
        }

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

        return Ok(new TaperRecommendationDto(
            eventDto, phase.Phase, phase.Message,
            phase.TargetPct, gymBaselineVolume, gymThisWeekVolume,
            phase.TargetPct, runBaselineDistance, runThisWeekDistance,
            tapersCompleted));
    }

    [HttpGet("events/{eventId}/checkins")]
    public async Task<ActionResult<List<TaperCheckInDto>>> GetCheckIns(int eventId)
    {
        var taperEvent = await _db.TaperEvents.FindAsync(eventId);
        if (taperEvent is null) return NotFound();

        var checkIns = await _db.TaperCheckIns
            .Where(c => c.TaperEventId == eventId)
            .OrderBy(c => c.Date)
            .ToListAsync();

        return Ok(checkIns.Select(c => ToCheckInDto(c, taperEvent.Date)).ToList());
    }

    [HttpPut("events/{eventId}/checkins")]
    public async Task<ActionResult<TaperCheckInDto>> UpsertCheckIn(int eventId, UpsertTaperCheckInRequest request)
    {
        var taperEvent = await _db.TaperEvents.FindAsync(eventId);
        if (taperEvent is null) return NotFound();

        var (windowStart, windowEnd) = TaperPhaseCalculator.CheckInWindow(taperEvent.Date, taperEvent.TaperDays);
        if (request.Date < windowStart || request.Date > windowEnd)
        {
            return BadRequest(new { error = $"Date must be within the taper/debrief window ({windowStart:yyyy-MM-dd} to {windowEnd:yyyy-MM-dd})." });
        }

        if (request.Energy is < 1 or > 5 || request.Soreness is < 1 or > 5 || request.Motivation is < 1 or > 5)
        {
            return BadRequest(new { error = "Energy, soreness, and motivation must each be between 1 and 5." });
        }

        var existing = await _db.TaperCheckIns.FirstOrDefaultAsync(c => c.TaperEventId == eventId && c.Date == request.Date);
        if (existing is null)
        {
            existing = new TaperCheckIn
            {
                TaperEventId = eventId,
                Date = request.Date,
                CreatedAt = DateTime.UtcNow,
            };
            _db.TaperCheckIns.Add(existing);
        }
        else
        {
            existing.UpdatedAt = DateTime.UtcNow;
        }

        existing.Energy = request.Energy;
        existing.Soreness = request.Soreness;
        existing.Motivation = request.Motivation;
        existing.Context = request.Context;

        await _db.SaveChangesAsync();
        return Ok(ToCheckInDto(existing, taperEvent.Date));
    }

    [HttpPost("events/{eventId}/consult")]
    public async Task<ActionResult<TaperConsultResponseDto>> Consult(int eventId, TaperConsultRequest request)
    {
        var taperEvent = await _db.TaperEvents.FindAsync(eventId);
        if (taperEvent is null) return NotFound();

        var question = string.IsNullOrWhiteSpace(request.Question)
            ? "Anything I should know about this taper?"
            : request.Question;

        try
        {
            var (answer, comparedToPriorTaper) = await _consultService.AskAsync(taperEvent, question);
            return Ok(new TaperConsultResponseDto(answer, comparedToPriorTaper));
        }
        catch (TaperConsultUnavailableException ex)
        {
            return StatusCode(503, new { error = ex.Message });
        }
    }
}
