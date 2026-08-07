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
            .OrderBy(e => e.Category).ThenBy(e => e.Name)
            .Select(e => new ExerciseDto(e.Id, e.Name, e.Category.ToString()))
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

        return Ok(new ExerciseDto(exercise.Id, exercise.Name, exercise.Category.ToString()));
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

        var entries = pageSessionIds
            .Select(sid =>
            {
                var sessionSets = sets.Where(s => s.WorkoutSessionId == sid).ToList();
                return new ExerciseHistoryEntryDto(
                    sid,
                    sessionSets[0].WorkoutSession.Date,
                    sessionSets.Select(s => new PreviousSetDto(s.SetOrder, s.Reps, s.WeightKg, s.SetType.ToString())).ToList());
            })
            .ToList();

        return Ok(new ExerciseHistoryPageDto(entries, allSessionIds.Count));
    }

    [HttpGet("{id}/stats")]
    public async Task<ActionResult<ExerciseStatsDto>> GetStats(int id)
    {
        var exercise = await _db.Exercises.FindAsync(id);
        if (exercise is null) return NotFound();

        var sets = await _db.ExerciseSets
            .Where(s => s.ExerciseId == id)
            .Include(s => s.WorkoutSession)
            .ToListAsync();

        if (sets.Count == 0)
        {
            return Ok(new ExerciseStatsDto(exercise.Name, 0, 0, 0, 0, []));
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

        return Ok(new ExerciseStatsDto(exercise.Name, heaviestWeight, bestEstimated1Rm, bestSetVolume, bestSessionVolume, chart));
    }
}
