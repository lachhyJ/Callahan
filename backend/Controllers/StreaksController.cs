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
public class StreaksController : ControllerBase
{
    private readonly AppDbContext _db;

    public StreaksController(AppDbContext db)
    {
        _db = db;
    }

    // Weekly rules, not daily — a missed single day shouldn't reset a streak
    // that's about training consistency, not attendance-taking.
    private static readonly (string Type, string Label, Func<int, int, bool> Qualifies)[] Definitions =
    [
        ("gym2", "2+ gym sessions", (gym, run) => gym >= 2),
        ("total3", "3+ sessions", (gym, run) => gym + run >= 3),
        ("gym3run1", "3 gym + a run", (gym, run) => gym >= 3 && run >= 1),
        ("run1", "1+ run", (gym, run) => run >= 1),
    ];

    [HttpGet]
    public async Task<ActionResult<List<StreakDto>>> GetStreaks()
    {
        var workoutDates = await _db.WorkoutSessions.Select(s => s.Date).ToListAsync();
        var runDates = await _db.Activities.Where(a => a.Type == ActivityType.Running).Select(a => a.Date).ToListAsync();

        if (workoutDates.Count == 0 && runDates.Count == 0)
        {
            return Ok(Definitions.Select(d => new StreakDto(d.Type, d.Label, 0, 0)).ToList());
        }

        var currentWeekStart = MondayOf(DateOnly.FromDateTime(DateTime.Now));
        var earliestWeekStart = MondayOf(workoutDates.Concat(runDates).Min());
        var weekCount = (currentWeekStart.DayNumber - earliestWeekStart.DayNumber) / 7 + 1;

        var gymCounts = new int[weekCount];
        var runCounts = new int[weekCount];
        foreach (var d in workoutDates) gymCounts[(MondayOf(d).DayNumber - earliestWeekStart.DayNumber) / 7]++;
        foreach (var d in runDates) runCounts[(MondayOf(d).DayNumber - earliestWeekStart.DayNumber) / 7]++;

        var results = Definitions.Select(def =>
        {
            var qualifies = new bool[weekCount];
            for (var i = 0; i < weekCount; i++) qualifies[i] = def.Qualifies(gymCounts[i], runCounts[i]);

            var best = 0;
            var run = 0;
            foreach (var q in qualifies)
            {
                run = q ? run + 1 : 0;
                best = Math.Max(best, run);
            }

            // The current (in-progress) week hasn't finished, so it can't yet
            // break a streak — fall back to last week when this week hasn't
            // qualified (yet), rather than treating it as a miss.
            var lastIndex = weekCount - 1;
            var startIndex = qualifies[lastIndex] ? lastIndex
                : lastIndex - 1 >= 0 && qualifies[lastIndex - 1] ? lastIndex - 1
                : -1;

            var current = 0;
            if (startIndex >= 0)
            {
                current = 1;
                var idx = startIndex - 1;
                while (idx >= 0 && qualifies[idx])
                {
                    current++;
                    idx--;
                }
            }

            return new StreakDto(def.Type, def.Label, current, best);
        }).ToList();

        return Ok(results);
    }

    [HttpGet("{type}")]
    public async Task<ActionResult<StreakDetailDto>> GetStreakDetail(string type)
    {
        var definition = Definitions.FirstOrDefault(d => d.Type == type);
        if (definition.Type is null) return NotFound();

        var workoutSessions = await _db.WorkoutSessions
            .Include(s => s.Sets).ThenInclude(set => set.Exercise)
            .Include(s => s.WorkoutTemplate)
            .OrderByDescending(s => s.Date)
            .ToListAsync();

        var workouts = workoutSessions.Select(s => new WorkoutSessionSummaryDto(
            s.Id, s.Date, s.Name, s.Notes, s.Sets.Count(set => set.SetType != SetType.Warmup),
            s.WorkoutTemplate != null ? s.WorkoutTemplate.Name : null,
            s.WorkoutTemplate != null ? s.WorkoutTemplate.Subtitle : null,
            s.StartedAt, s.FinishedAt,
            WorkoutSessionsController.CategorySummary(s.Sets))).ToList();

        var runs = await _db.Activities
            .Where(a => a.Type == ActivityType.Running)
            .OrderByDescending(a => a.Date)
            .Select(a => new ActivityDto(a.Id, a.Date, a.Type.ToString(), a.Source.ToString(), a.DurationSeconds, a.DistanceKm, a.Calories, a.AvgHeartRate, a.Notes,
                a.RunSessionTypeId, a.RunSessionType == null ? null : a.RunSessionType.Name))
            .ToListAsync();

        if (workouts.Count == 0 && runs.Count == 0)
        {
            return Ok(new StreakDetailDto(definition.Type, definition.Label, []));
        }

        var currentWeekStart = MondayOf(DateOnly.FromDateTime(DateTime.Now));
        var earliestWeekStart = MondayOf(workouts.Select(w => w.Date).Concat(runs.Select(r => r.Date)).Min());
        var weekCount = (currentWeekStart.DayNumber - earliestWeekStart.DayNumber) / 7 + 1;

        var weeks = new List<StreakWeekDto>();
        for (var i = weekCount - 1; i >= 0; i--)
        {
            var weekStart = earliestWeekStart.AddDays(7 * i);
            var weekEnd = weekStart.AddDays(6);
            var weekWorkouts = workouts.Where(w => w.Date >= weekStart && w.Date <= weekEnd).ToList();
            var weekRuns = runs.Where(r => r.Date >= weekStart && r.Date <= weekEnd).ToList();
            weeks.Add(new StreakWeekDto(weekStart, definition.Qualifies(weekWorkouts.Count, weekRuns.Count), weekWorkouts, weekRuns));
        }

        return Ok(new StreakDetailDto(definition.Type, definition.Label, weeks));
    }

    // Monday-first week start, matching the frontend's convention (dateUtils.js).
    private static DateOnly MondayOf(DateOnly date)
    {
        var offsetFromMonday = ((int)date.DayOfWeek + 6) % 7; // Mon=0 ... Sun=6
        return date.AddDays(-offsetFromMonday);
    }
}
