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
public class StreaksController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly TimeProvider _time;

    public StreaksController(AppDbContext db, TimeProvider? time = null)
    {
        _db = db;
        _time = time ?? TimeProvider.System;
    }

    // Weekly rules, not daily — a missed single day shouldn't reset a streak
    // that's about training consistency, not attendance-taking. Definitions
    // and week-bucketing live in WeeklyConsistencyService so MonthlyReportBuilder
    // can reuse the exact same rules.
    private static readonly WeeklyConsistencyDefinition[] Definitions = WeeklyConsistencyService.Definitions;

    [HttpGet]
    public async Task<ActionResult<List<StreakDto>>> GetStreaks()
    {
        var workoutDates = await _db.WorkoutSessions.Select(s => s.Date).ToListAsync();
        var runDates = await _db.Activities.Where(a => a.Type == ActivityType.Running).Select(a => a.Date).ToListAsync();

        if (workoutDates.Count == 0 && runDates.Count == 0)
        {
            return Ok(Definitions.Select(d => new StreakDto(d.Type, d.Label, 0, 0)).ToList());
        }

        var currentWeekStart = MondayOf(_time.Today());
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

            var (current, best) = WeeklyConsistencyService.Streak(qualifies);
            return new StreakDto(def.Type, def.Label, current, best);
        }).ToList();

        return Ok(results);
    }

    [HttpGet("{type}")]
    public async Task<ActionResult<StreakDetailDto>> GetStreakDetail(string type)
    {
        var definition = Definitions.FirstOrDefault(d => d.Type == type);
        if (definition is null) return NotFound();

        var workoutSessions = await _db.WorkoutSessions
            .Include(s => s.Sets).ThenInclude(set => set.Exercise)
            .Include(s => s.WorkoutTemplate)
            .OrderByDescending(s => s.Date)
            .ToListAsync();

        var workouts = workoutSessions.Select(WorkoutSessionsController.ToSummaryDto).ToList();

        var runs = await _db.Activities
            .Where(a => a.Type == ActivityType.Running)
            .OrderByDescending(a => a.Date)
            .Select(ActivitiesController.ListProjection)
            .ToListAsync();

        if (workouts.Count == 0 && runs.Count == 0)
        {
            return Ok(new StreakDetailDto(definition.Type, definition.Label, []));
        }

        var currentWeekStart = MondayOf(_time.Today());
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

    private static DateOnly MondayOf(DateOnly date) => CalendarDates.MondayOf(date);
}
