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

    private const int RecoveryWindowDays = 7;

    // Runs on the two most-hit read paths (the active list and the deleted
    // list itself) rather than on a schedule — good enough for a handful of
    // rows nobody's in a hurry to see gone, without a NAS cron job to maintain.
    private async Task PurgeExpiredAsync()
    {
        var cutoff = DateTime.UtcNow.AddDays(-RecoveryWindowDays);
        var expiredIds = await _db.WorkoutSessions.IgnoreQueryFilters()
            .Where(s => s.DeletedAt != null && s.DeletedAt < cutoff)
            .Select(s => s.Id)
            .ToListAsync();
        if (expiredIds.Count == 0) return;

        // WorkoutSession has no ExerciseNotes navigation, so the change
        // tracker can't cascade that table on its own — clear it explicitly
        // alongside Sets rather than leaving orphaned notes behind.
        _db.ExerciseSets.RemoveRange(await _db.ExerciseSets.Where(s => expiredIds.Contains(s.WorkoutSessionId)).ToListAsync());
        _db.ExerciseNotes.RemoveRange(await _db.ExerciseNotes.Where(n => expiredIds.Contains(n.WorkoutSessionId)).ToListAsync());
        _db.WorkoutSessions.RemoveRange(await _db.WorkoutSessions.IgnoreQueryFilters().Where(s => expiredIds.Contains(s.Id)).ToListAsync());
        await _db.SaveChangesAsync();
    }

    [HttpGet]
    public async Task<ActionResult<List<WorkoutSessionSummaryDto>>> GetAll(DateOnly? start = null, DateOnly? end = null)
    {
        await PurgeExpiredAsync();

        var query = _db.WorkoutSessions.AsQueryable();
        if (start is not null) query = query.Where(s => s.Date >= start);
        if (end is not null) query = query.Where(s => s.Date <= end);

        var sessions = await query
            .Include(s => s.Sets).ThenInclude(set => set.Exercise)
            .Include(s => s.WorkoutTemplate)
            .OrderByDescending(s => s.Date)
            .ToListAsync();

        // SetCount is working sets only, matching MuscleGroupsController/StreaksController's
        // volume calculations — warmups don't count toward the prescribed/working load this
        // number is meant to reflect (decided when default program warmups were added).
        var result = sessions.Select(s => new WorkoutSessionSummaryDto(
            s.Id, s.Date, s.Name, s.Notes, s.Sets.Count(set => set.SetType != SetType.Warmup),
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
            .Include(s => s.WorkoutTemplate)
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
            session.Name,
            session.Notes,
            session.StartedAt,
            session.FinishedAt,
            session.WorkoutTemplate?.Name,
            session.WorkoutTemplate?.Subtitle,
            CategorySummary(session.Sets),
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

        // SetOrder is assigned here, not trusted from the client: the 0-based
        // position of each set within its exercise, in the order sent (which is
        // display / logged order). Guarantees a unique, contiguous, 0-based
        // sequence per exercise - the client has drifted between 0- and 1-based
        // (the 2026-08-22 warmup change), and an early import produced colliding
        // values. Both history views render `Set {setOrder + 1}`, so 0-based is
        // the correct convention.
        var nextOrder = new Dictionary<int, int>();
        var sets = request.Sets.Select(s =>
        {
            int order = nextOrder.GetValueOrDefault(s.ExerciseId);
            nextOrder[s.ExerciseId] = order + 1;
            return new ExerciseSet
            {
                ExerciseId = s.ExerciseId,
                Reps = s.Reps,
                WeightKg = s.WeightKg,
                SetOrder = order,
                SetType = Enum.Parse<SetType>(s.SetType, ignoreCase: true)
            };
        }).ToList();

        var session = new WorkoutSession
        {
            Date = request.Date,
            Name = request.Name,
            Notes = request.Notes,
            WorkoutTemplateId = request.WorkoutTemplateId,
            StartedAt = request.StartedAt,
            FinishedAt = request.FinishedAt,
            Sets = sets
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

    [HttpPut("{id}/name")]
    public async Task<IActionResult> UpdateName(int id, UpdateWorkoutSessionNameRequest request)
    {
        var session = await _db.WorkoutSessions.FindAsync(id);
        if (session is null) return NotFound();

        session.Name = string.IsNullOrWhiteSpace(request.Name) ? null : request.Name.Trim();
        await _db.SaveChangesAsync();

        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var session = await _db.WorkoutSessions.FirstOrDefaultAsync(s => s.Id == id);
        if (session is null) return NotFound();

        session.DeletedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return NoContent();
    }

    [HttpGet("deleted")]
    public async Task<ActionResult<List<DeletedWorkoutSessionDto>>> GetDeleted()
    {
        await PurgeExpiredAsync();

        var sessions = await _db.WorkoutSessions.IgnoreQueryFilters()
            .Where(s => s.DeletedAt != null)
            .Include(s => s.Sets).ThenInclude(set => set.Exercise)
            .Include(s => s.WorkoutTemplate)
            .OrderByDescending(s => s.DeletedAt)
            .ToListAsync();

        var result = sessions.Select(s => new DeletedWorkoutSessionDto(
            s.Id, s.Date, s.Name, s.Sets.Count(set => set.SetType != SetType.Warmup),
            s.WorkoutTemplate != null ? s.WorkoutTemplate.Name : null,
            s.WorkoutTemplate != null ? s.WorkoutTemplate.Subtitle : null,
            CategorySummary(s.Sets),
            s.DeletedAt!.Value)).ToList();

        return Ok(result);
    }

    [HttpPost("{id}/restore")]
    public async Task<ActionResult<WorkoutSessionSummaryDto>> Restore(int id)
    {
        var session = await _db.WorkoutSessions.IgnoreQueryFilters()
            .Include(s => s.Sets).ThenInclude(set => set.Exercise)
            .Include(s => s.WorkoutTemplate)
            .FirstOrDefaultAsync(s => s.Id == id && s.DeletedAt != null);
        if (session is null) return NotFound();

        session.DeletedAt = null;
        await _db.SaveChangesAsync();

        return Ok(new WorkoutSessionSummaryDto(
            session.Id, session.Date, session.Name, session.Notes, session.Sets.Count(set => set.SetType != SetType.Warmup),
            session.WorkoutTemplate?.Name, session.WorkoutTemplate?.Subtitle,
            session.StartedAt, session.FinishedAt,
            CategorySummary(session.Sets)));
    }
}
