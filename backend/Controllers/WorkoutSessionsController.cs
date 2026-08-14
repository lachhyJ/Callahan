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
public class WorkoutSessionsController : ControllerBase
{
    private readonly AppDbContext _db;

    public WorkoutSessionsController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<ActionResult<List<WorkoutSessionSummaryDto>>> GetAll()
    {
        var sessions = await _db.WorkoutSessions
            .Include(s => s.Sets).ThenInclude(set => set.Exercise)
            .Include(s => s.WorkoutTemplate)
            .OrderByDescending(s => s.Date)
            .ToListAsync();

        var result = sessions.Select(s => new WorkoutSessionSummaryDto(
            s.Id, s.Date, s.Notes, s.Sets.Count,
            s.WorkoutTemplate != null ? s.WorkoutTemplate.Name : null,
            s.WorkoutTemplate != null ? s.WorkoutTemplate.Subtitle : null,
            s.StartedAt, s.FinishedAt,
            CategorySummary(s.Sets))).ToList();

        return Ok(result);
    }

    // Untitled (template-less) sessions fall back to this — and in practice
    // that's every session, since none of the real history carries a
    // WorkoutTemplateId (Hevy-imported, not started via the templated flow).
    // Exercise names ran on forever for a 6-exercise session; category is
    // short, always available, and closer to how the program names its own
    // days ("Lower & Power") than a growing exercise list ever was.
    internal static string? CategorySummary(ICollection<ExerciseSet> sets)
    {
        if (sets.Count == 0) return null;
        var categories = sets
            .GroupBy(s => s.Exercise.Category)
            .OrderByDescending(g => g.Count())
            .Select(g => g.Key.ToString())
            .ToList();
        return string.Join(", ", categories);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<WorkoutSessionDetailDto>> GetById(int id)
    {
        var session = await _db.WorkoutSessions
            .Include(s => s.Sets).ThenInclude(set => set.Exercise)
            .FirstOrDefaultAsync(s => s.Id == id);

        if (session is null) return NotFound();

        var notes = await _db.ExerciseNotes
            .Where(n => n.WorkoutSessionId == id)
            .Include(n => n.Exercise)
            .Select(n => new ExerciseNoteDto(n.ExerciseId, n.Exercise.Name, n.Notes))
            .ToListAsync();

        var dto = new WorkoutSessionDetailDto(
            session.Id,
            session.Date,
            session.Notes,
            session.StartedAt,
            session.FinishedAt,
            session.Sets
                .OrderBy(set => set.SetOrder)
                .Select(set => new ExerciseSetDto(set.Id, set.ExerciseId, set.Exercise.Name, set.Reps, set.WeightKg, set.SetOrder, set.SetType.ToString()))
                .ToList(),
            notes);

        return Ok(dto);
    }

    // Monday-first week start, matching the frontend's convention (dateUtils.js).
    private static DateOnly MondayOf(DateOnly date)
    {
        var offsetFromMonday = ((int)date.DayOfWeek + 6) % 7; // Mon=0 ... Sun=6
        return date.AddDays(-offsetFromMonday);
    }

    [HttpGet("weekly-volume")]
    public async Task<ActionResult<List<WeeklyVolumeDto>>> GetWeeklyVolume([FromQuery] int weeks = 8)
    {
        var today = DateOnly.FromDateTime(DateTime.Now);
        var currentWeekStart = MondayOf(today);
        var earliestWeekStart = currentWeekStart.AddDays(-7 * (weeks - 1));

        var sets = await _db.ExerciseSets
            .Where(s => s.WorkoutSession.Date >= earliestWeekStart)
            .Include(s => s.WorkoutSession)
            .ToListAsync();

        var volumesByWeek = new Dictionary<DateOnly, decimal>();
        for (var i = 0; i < weeks; i++)
        {
            volumesByWeek[earliestWeekStart.AddDays(7 * i)] = 0;
        }

        foreach (var s in sets)
        {
            var weekStart = MondayOf(s.WorkoutSession.Date);
            if (volumesByWeek.ContainsKey(weekStart))
            {
                volumesByWeek[weekStart] += s.WeightKg * s.Reps;
            }
        }

        var result = volumesByWeek
            .OrderBy(kv => kv.Key)
            .Select(kv => new WeeklyVolumeDto(kv.Key, kv.Value))
            .ToList();

        return Ok(result);
    }

    [HttpPost]
    public async Task<ActionResult<WorkoutSessionDetailDto>> Create(CreateWorkoutSessionRequest request)
    {
        if (request.Sets.Any(s => !Enum.TryParse<SetType>(s.SetType, ignoreCase: true, out _)))
        {
            return BadRequest(new { error = "Unknown set type." });
        }

        var session = new WorkoutSession
        {
            Date = request.Date,
            Notes = request.Notes,
            WorkoutTemplateId = request.WorkoutTemplateId,
            StartedAt = request.StartedAt,
            FinishedAt = request.FinishedAt,
            Sets = request.Sets.Select(s => new ExerciseSet
            {
                ExerciseId = s.ExerciseId,
                Reps = s.Reps,
                WeightKg = s.WeightKg,
                SetOrder = s.SetOrder,
                SetType = Enum.Parse<SetType>(s.SetType, ignoreCase: true)
            }).ToList()
        };

        _db.WorkoutSessions.Add(session);
        await _db.SaveChangesAsync();

        if (request.ExerciseNotes is { Count: > 0 })
        {
            var notes = request.ExerciseNotes
                .Where(n => !string.IsNullOrWhiteSpace(n.Notes))
                .Select(n => new ExerciseNote
                {
                    WorkoutSessionId = session.Id,
                    ExerciseId = n.ExerciseId,
                    Notes = n.Notes
                });

            _db.ExerciseNotes.AddRange(notes);
            await _db.SaveChangesAsync();
        }

        return await GetById(session.Id);
    }
}
