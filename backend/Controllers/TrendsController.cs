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

        var runDates = await _db.Activities
            .Where(a => a.Type == ActivityType.Running && a.Date >= earliestMonthStart)
            .Select(a => a.Date)
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

    // Movement, not bests — deliberately excludes exercises with only one
    // month of data in the window, since a single session can't show a trend.
    [HttpGet("exercises")]
    public async Task<ActionResult<List<LiftTrendDto>>> GetLiftTrends([FromQuery] int months = 6)
    {
        var today = DateOnly.FromDateTime(DateTime.Now);
        var currentMonthStart = new DateOnly(today.Year, today.Month, 1);
        var earliestMonthStart = currentMonthStart.AddMonths(-(months - 1));

        var sets = await _db.ExerciseSets
            .Where(s => s.WorkoutSession.Date >= earliestMonthStart)
            .Include(s => s.WorkoutSession)
            .Include(s => s.Exercise)
            .ToListAsync();

        var maxWeightByExerciseAndMonth = new Dictionary<int, Dictionary<DateOnly, decimal>>();
        var exerciseNames = new Dictionary<int, string>();

        foreach (var s in sets)
        {
            exerciseNames[s.ExerciseId] = s.Exercise.Name;
            var monthStart = new DateOnly(s.WorkoutSession.Date.Year, s.WorkoutSession.Date.Month, 1);

            if (!maxWeightByExerciseAndMonth.TryGetValue(s.ExerciseId, out var byMonth))
            {
                byMonth = [];
                maxWeightByExerciseAndMonth[s.ExerciseId] = byMonth;
            }

            byMonth[monthStart] = byMonth.TryGetValue(monthStart, out var existing) ? Math.Max(existing, s.WeightKg) : s.WeightKg;
        }

        var trends = new List<LiftTrendDto>();
        foreach (var (exerciseId, byMonth) in maxWeightByExerciseAndMonth)
        {
            if (byMonth.Count < 2) continue;

            var months2 = byMonth.Keys.OrderBy(m => m).ToList();
            var earliestMonth = months2[0];
            var latestMonth = months2[^1];
            var earliestWeight = byMonth[earliestMonth];
            var latestWeight = byMonth[latestMonth];

            trends.Add(new LiftTrendDto(exerciseId, exerciseNames[exerciseId], earliestWeight, earliestMonth, latestWeight, latestMonth, latestWeight - earliestWeight));
        }

        return Ok(trends.OrderByDescending(t => Math.Abs(t.DeltaKg)).ToList());
    }

    // Count per type first (what Lachlan asked to see), plus distance so
    // e.g. "how far am I actually covering in High Speed Intervals sessions"
    // has an answer — that's the whole-session distance, not a breakdown of
    // just the sprint portions, since Garmin sync only pulls aggregate
    // distance/duration per activity, not lap-level splits.
    [HttpGet("runs")]
    public async Task<ActionResult<List<RunTypeTrendDto>>> GetRunTypeTrends([FromQuery] int months = 6)
    {
        var today = DateOnly.FromDateTime(DateTime.Now);
        var currentMonthStart = new DateOnly(today.Year, today.Month, 1);
        var earliestMonthStart = currentMonthStart.AddMonths(-(months - 1));

        var runs = await _db.Activities
            .Where(a => a.Type == ActivityType.Running && a.Date >= earliestMonthStart && a.ActivitySessionTypeId != null)
            .Include(a => a.ActivitySessionType)
            .ToListAsync();

        var trends = runs
            .GroupBy(a => new { a.ActivitySessionTypeId, Name = a.ActivitySessionType!.Name })
            .Select(g =>
            {
                var withDistance = g.Where(a => a.DistanceKm != null).ToList();
                var totalDistance = withDistance.Sum(a => a.DistanceKm!.Value);
                return new RunTypeTrendDto(
                    g.Key.ActivitySessionTypeId!.Value, g.Key.Name, g.Count(),
                    totalDistance, withDistance.Count > 0 ? totalDistance / withDistance.Count : 0);
            })
            .OrderByDescending(t => t.SessionCount)
            .ToList();

        return Ok(trends);
    }
}
