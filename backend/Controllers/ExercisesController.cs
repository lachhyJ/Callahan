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
public class ExercisesController : ControllerBase
{
    private readonly AppDbContext _db;

    public ExercisesController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<ActionResult<List<ExerciseDto>>> GetAll()
    {
        var exercises = await _db.Exercises
            .Include(e => e.MuscleTargets)
            .OrderBy(e => e.Category).ThenBy(e => e.Name)
            .Select(e => new ExerciseDto(
                e.Id,
                e.Name,
                e.Category.ToString(),
                e.MuscleTargets.Where(mt => mt.IsPrimary).Select(mt => mt.MuscleGroup.ToString()).FirstOrDefault(),
                e.IsAssisted,
                e.IsTimeBased,
                e.IsPerSide,
                e.PerSideDelaySeconds))
            .ToListAsync();

        return Ok(exercises);
    }

    [HttpGet("pickable")]
    public async Task<ActionResult<List<PickableExerciseDto>>> GetPickable()
    {
        var templateMemberships = await _db.WorkoutTemplateExercises
            .Include(wte => wte.WorkoutTemplate)
            .Select(wte => new { wte.ExerciseId, TemplateName = wte.WorkoutTemplate.Name })
            .Distinct()
            .ToListAsync();
        var templateNamesByExercise = templateMemberships
            .GroupBy(m => m.ExerciseId)
            .ToDictionary(g => g.Key, g => g.Select(m => m.TemplateName).ToList());

        var exercises = await _db.Exercises
            .OrderBy(e => e.Category).ThenBy(e => e.Name)
            .Select(e => new { e.Id, e.Name, e.Category, e.IsAssisted, e.IsTimeBased, e.IsPerSide, e.PerSideDelaySeconds })
            .ToListAsync();

        var result = exercises
            .Select(e => new PickableExerciseDto(
                e.Id, e.Name, e.Category.ToString(), e.IsAssisted, e.IsTimeBased, e.IsPerSide, e.PerSideDelaySeconds,
                templateNamesByExercise.GetValueOrDefault(e.Id, [])))
            .ToList();

        return Ok(result);
    }

    [HttpPost]
    public async Task<ActionResult<ExerciseDto>> Create(CreateExerciseRequestDto request)
    {
        if (!Enum.TryParse<ExerciseCategory>(request.Category, ignoreCase: true, out var category))
        {
            return BadRequest(new { error = $"Unknown category '{request.Category}'." });
        }

        var exercise = new Exercise { Name = request.Name, Category = category };
        _db.Exercises.Add(exercise);
        await _db.SaveChangesAsync();

        return Ok(new ExerciseDto(exercise.Id, exercise.Name, exercise.Category.ToString(), null, exercise.IsAssisted, exercise.IsTimeBased, exercise.IsPerSide, exercise.PerSideDelaySeconds));
    }

    [HttpPut("{id}/assisted")]
    public async Task<IActionResult> UpdateAssisted(int id, UpdateExerciseAssistedRequestDto request)
    {
        var exercise = await _db.Exercises.FindAsync(id);
        if (exercise is null) return NotFound();

        exercise.IsAssisted = request.IsAssisted;
        await _db.SaveChangesAsync();

        return NoContent();
    }

    // Time-based (held for time, not counted in reps) and its per-side flag are
    // set together from the exercise detail screen, next to the assisted toggle.
    // IsPerSide only means anything while IsTimeBased is on, but it's stored
    // independently so turning time-based off and back on doesn't lose it.
    // PerSideDelaySeconds (the gap before side two auto-starts) rides along on
    // the same request and is clamped to 0..60.
    [HttpPut("{id}/time-based")]
    public async Task<IActionResult> UpdateTimeBased(int id, UpdateExerciseTimeBasedRequestDto request)
    {
        var exercise = await _db.Exercises.FindAsync(id);
        if (exercise is null) return NotFound();

        exercise.IsTimeBased = request.IsTimeBased;
        exercise.IsPerSide = request.IsPerSide;
        exercise.PerSideDelaySeconds = Math.Clamp(request.PerSideDelaySeconds, 0, 60);
        await _db.SaveChangesAsync();

        return NoContent();
    }

    [HttpPut("{id}/name")]
    public async Task<IActionResult> UpdateName(int id, UpdateExerciseNameRequestDto request)
    {
        var name = request.Name.Trim();
        if (name.Length == 0) return BadRequest(new { error = "Name can't be empty." });

        var exercise = await _db.Exercises.FindAsync(id);
        if (exercise is null) return NotFound();

        exercise.Name = name;
        await _db.SaveChangesAsync();

        return NoContent();
    }

    [HttpGet("{id}/history")]
    public async Task<ActionResult<ExerciseHistoryPageDto>> GetHistory(int id, [FromQuery] int limit = 10, [FromQuery] int offset = 0)
    {
        var allSessionIds = await _db.ExerciseSets
            .Where(s => s.ExerciseId == id)
            .Select(s => s.WorkoutSessionId)
            .Distinct()
            .Join(_db.WorkoutSessions, sid => sid, ws => ws.Id, (sid, ws) => new { sid, ws.Date })
            .OrderByDescending(x => x.Date)
            .ThenByDescending(x => x.sid)
            .Select(x => x.sid)
            .ToListAsync();

        var pageSessionIds = allSessionIds.Skip(offset).Take(limit).ToList();

        var sets = await _db.ExerciseSets
            .Where(s => s.ExerciseId == id && pageSessionIds.Contains(s.WorkoutSessionId))
            .Include(s => s.WorkoutSession)
            .OrderBy(s => s.SetOrder)
            .ToListAsync();

        var notes = await _db.ExerciseNotes
            .Where(n => n.ExerciseId == id && pageSessionIds.Contains(n.WorkoutSessionId))
            .ToDictionaryAsync(n => n.WorkoutSessionId, n => n.Notes);

        var entries = pageSessionIds
            .Select(sid =>
            {
                var sessionSets = sets.Where(s => s.WorkoutSessionId == sid).ToList();
                return new ExerciseHistoryEntryDto(
                    sid,
                    sessionSets[0].WorkoutSession.Date,
                    notes.GetValueOrDefault(sid),
                    sessionSets.Select(s => new PreviousSetDto(s.SetOrder, s.Reps, s.WeightKg, s.SetType.ToString(), s.DurationSeconds)).ToList());
            })
            .ToList();

        return Ok(new ExerciseHistoryPageDto(entries, allSessionIds.Count));
    }

    [HttpGet("{id}/cues")]
    public async Task<ActionResult<List<ExerciseCueDto>>> GetCues(int id)
    {
        var cues = await _db.WorkoutTemplateExercises
            .Where(te => te.ExerciseId == id)
            .Include(te => te.WorkoutTemplate)
            .Select(te => new ExerciseCueDto(te.Id, te.WorkoutTemplate.Name, te.Cue))
            .ToListAsync();

        return Ok(cues);
    }

    [HttpGet("{id}/stats")]
    public async Task<ActionResult<ExerciseStatsDto>> GetStats(int id)
    {
        var exercise = await _db.Exercises.Include(e => e.MuscleTargets).FirstOrDefaultAsync(e => e.Id == id);
        if (exercise is null) return NotFound();

        var primaryMuscle = exercise.MuscleTargets.Where(mt => mt.IsPrimary).Select(mt => mt.MuscleGroup.ToString()).FirstOrDefault();

        // Time sets carry no load or rep count (Reps = 0, WeightKg = 0), so they
        // contribute nothing to any of the figures below and would only drag a
        // Max or a basis toward zero - excluded here, same as every other
        // strength read site.
        var sets = await _db.ExerciseSets
            .Where(s => s.ExerciseId == id && s.DurationSeconds == null)
            .Include(s => s.WorkoutSession)
            .ToListAsync();

        if (sets.Count == 0)
        {
            return Ok(new ExerciseStatsDto(exercise.Name, primaryMuscle, exercise.IsAssisted, exercise.IsTimeBased, exercise.IsPerSide, exercise.PerSideDelaySeconds, 0, 0, 0, 0, []));
        }

        var heaviestWeight = sets.Max(s => s.WeightKg);
        var bestEstimated1Rm = sets.Max(s => LiftMath.Epley1Rm(s.Reps, s.WeightKg));
        var bestSetVolume = sets.Max(s => s.WeightKg * s.Reps);

        var sessionVolumes = sets
            .GroupBy(s => s.WorkoutSessionId)
            .Select(g => g.Sum(s => s.WeightKg * s.Reps));
        var bestSessionVolume = sessionVolumes.Max();

        var chart = sets
            .GroupBy(s => s.WorkoutSessionId)
            .Select(g => new { g.First().WorkoutSession.Date, MaxWeight = g.Max(s => s.WeightKg) })
            .OrderBy(x => x.Date)
            .Select(x => new ChartPointDto(x.Date, x.MaxWeight))
            .ToList();

        return Ok(new ExerciseStatsDto(exercise.Name, primaryMuscle, exercise.IsAssisted, exercise.IsTimeBased, exercise.IsPerSide, exercise.PerSideDelaySeconds, heaviestWeight, bestEstimated1Rm, bestSetVolume, bestSessionVolume, chart));
    }
}
