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
                e.MuscleTargets.Where(mt => mt.IsPrimary).Select(mt => mt.MuscleGroup.ToString()).FirstOrDefault()))
            .ToListAsync();

        return Ok(exercises);
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

        return Ok(new ExerciseDto(exercise.Id, exercise.Name, exercise.Category.ToString(), null));
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
                    sessionSets.Select(s => new PreviousSetDto(s.SetOrder, s.Reps, s.WeightKg, s.SetType.ToString())).ToList());
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

    [HttpGet("prs")]
    public async Task<ActionResult<List<ExercisePrDto>>> GetPrs()
    {
        var sets = await _db.ExerciseSets
            .Include(s => s.Exercise).ThenInclude(e => e.MuscleTargets)
            .Include(s => s.WorkoutSession)
            .ToListAsync();

        var prs = sets
            .GroupBy(s => s.ExerciseId)
            .Select(g =>
            {
                var exercise = g.First().Exercise;
                var primaryMuscle = exercise.MuscleTargets.Where(mt => mt.IsPrimary).Select(mt => mt.MuscleGroup.ToString()).FirstOrDefault();
                var best = g.OrderByDescending(s => s.WeightKg).ThenBy(s => s.WorkoutSession.Date).First();
                var bestEstimated1Rm = g.Max(s => s.WeightKg * (1 + s.Reps / 30m));

                return new ExercisePrDto(
                    exercise.Id,
                    exercise.Name,
                    exercise.Category.ToString(),
                    primaryMuscle,
                    best.WeightKg,
                    best.WorkoutSession.Date,
                    bestEstimated1Rm);
            })
            .OrderByDescending(p => p.HeaviestWeightDate)
            .ToList();

        return Ok(prs);
    }

    [HttpGet("{id}/stats")]
    public async Task<ActionResult<ExerciseStatsDto>> GetStats(int id)
    {
        var exercise = await _db.Exercises.Include(e => e.MuscleTargets).FirstOrDefaultAsync(e => e.Id == id);
        if (exercise is null) return NotFound();

        var primaryMuscle = exercise.MuscleTargets.Where(mt => mt.IsPrimary).Select(mt => mt.MuscleGroup.ToString()).FirstOrDefault();

        var sets = await _db.ExerciseSets
            .Where(s => s.ExerciseId == id)
            .Include(s => s.WorkoutSession)
            .ToListAsync();

        if (sets.Count == 0)
        {
            return Ok(new ExerciseStatsDto(exercise.Name, primaryMuscle, 0, 0, 0, 0, []));
        }

        var heaviestWeight = sets.Max(s => s.WeightKg);
        // Epley formula: 1RM = weight * (1 + reps/30). Rounded up to the nearest whole rep.
        var bestEstimated1Rm = sets.Max(s => s.WeightKg * (1 + s.Reps / 30m));
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

        return Ok(new ExerciseStatsDto(exercise.Name, primaryMuscle, heaviestWeight, bestEstimated1Rm, bestSetVolume, bestSessionVolume, chart));
    }
}
