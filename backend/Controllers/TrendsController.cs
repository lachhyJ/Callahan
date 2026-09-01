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

        // Each exercise's basis comes from its own history (LiftProgress):
        // e1RM normally, set volume for high-rep accessories, load-then-reps
        // for assisted/bodyweight. This used to take Math.Max(WeightKg) per
        // month, which is blind to double progression - grinding 240x10 up to
        // 240x12 showed as +0 kg - and pointed backwards on assisted lifts,
        // where a bigger number means more help.
        var byExercise = sets
            .GroupBy(s => s.ExerciseId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var trends = new List<LiftTrendDto>();
        foreach (var (exerciseId, exerciseSets) in byExercise)
        {
            var basis = LiftProgress.BasisFor(exerciseSets.Select(ToInput).ToList());

            var bestByMonth = exerciseSets
                .GroupBy(s => new DateOnly(s.WorkoutSession.Date.Year, s.WorkoutSession.Date.Month, 1))
                .ToDictionary(
                    g => g.Key,
                    g => LiftProgress.Best(g.Select(ToInput).ToList(), basis)!);

            if (bestByMonth.Count < 2) continue;

            var months2 = bestByMonth.Keys.OrderBy(m => m).ToList();
            var earliestMonth = months2[0];
            var latestMonth = months2[^1];
            var earliest = bestByMonth[earliestMonth];
            var latest = bestByMonth[latestMonth];

            trends.Add(new LiftTrendDto(
                exerciseId, exerciseSets[0].Exercise.Name,
                LiftProgress.ToDto(earliest, basis), earliestMonth,
                LiftProgress.ToDto(latest, basis), latestMonth,
                LiftProgress.DeltaPercent(earliest, latest, basis) is decimal d ? Math.Round(d, 1) : null,
                latest.WeightKg - earliest.WeightKg,
                basis.ToString()));
        }

        return Ok(trends
            .OrderByDescending(t => t.DeltaPercent is null ? 0m : Math.Abs(t.DeltaPercent.Value))
            .ThenByDescending(t => Math.Abs(t.DeltaKg))
            .ToList());
    }

    // Count per type first (what Lachlan asked to see), plus whichever of
    // distance / high-speed distance / work reps actually means something for
    // that type — RunningMetrics.ShapeFor is the shared rule, so this agrees
    // with the monthly report's running section. Whole-session distance used
    // to be shown for every type on the grounds that Garmin gave us nothing
    // finer; lap-level splits landed 2026-08-25, and for the rep-based types
    // that total was actively misleading (GPS under-measures shuttle turns,
    // elapsed time counts standing rest).
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

        var activeLapCounts = await _db.ActivityLaps
            .Where(l => l.IntensityType == ActivityLap.ActiveIntensityType
                     && l.Activity.Type == ActivityType.Running && l.Activity.Date >= earliestMonthStart)
            .GroupBy(l => l.ActivityId)
            .Select(g => new { ActivityId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.ActivityId, x => x.Count);

        var trends = runs
            .GroupBy(a => new { a.ActivitySessionTypeId, Name = a.ActivitySessionType!.Name })
            .Select(g =>
            {
                var shape = RunningMetrics.ShapeFor(g.Key.Name);

                decimal? total = null, avg = null;
                if (shape.Distance)
                {
                    var withDistance = g.Where(a => a.DistanceKm != null).ToList();
                    total = withDistance.Sum(a => a.DistanceKm!.Value);
                    avg = withDistance.Count > 0 ? total / withDistance.Count : 0m;
                }

                decimal? highSpeedKm = null;
                if (shape.HighSpeed)
                {
                    var withHighSpeed = g.Where(a => a.HighSpeedDistanceM != null).ToList();
                    if (withHighSpeed.Count > 0)
                    {
                        highSpeedKm = Math.Round(withHighSpeed.Sum(a => a.HighSpeedDistanceM!.Value) / 1000m, 2);
                    }
                }

                int? reps = null;
                if (shape.Reps)
                {
                    var totalReps = g.Sum(a => activeLapCounts.TryGetValue(a.Id, out var n) ? n : 0);
                    if (totalReps > 0) reps = totalReps;
                }

                return new RunTypeTrendDto(
                    g.Key.ActivitySessionTypeId!.Value, g.Key.Name, g.Count(),
                    total, avg, highSpeedKm, reps);
            })
            .OrderByDescending(t => t.SessionCount)
            .ToList();

        return Ok(trends);
    }

    // Strength through the season: per-lift monthly best e1RM as % change from
    // its own baseline month, with monthly run / Ultimate load and the
    // season / tournament bands to draw behind it. Descriptive only.
    [HttpGet("season-strength")]
    public async Task<ActionResult<SeasonStrengthDto>> GetSeasonStrength([FromQuery] int months = 9)
    {
        months = Math.Clamp(months, 1, 24);
        var today = DateOnly.FromDateTime(DateTime.Now);
        var currentMonthStart = new DateOnly(today.Year, today.Month, 1);
        var earliestMonthStart = currentMonthStart.AddMonths(-(months - 1));

        // The current program: every exercise in any Day A/B/C template, keyed
        // to its shallowest slot position (1 = first lift of a session). The
        // chart tracks only these, and starts with the compounds (slot <= 2)
        // visible.
        var programSlots = await _db.WorkoutTemplateExercises
            .GroupBy(te => te.ExerciseId)
            .Select(g => new { ExerciseId = g.Key, Order = g.Min(te => te.ExerciseOrder) })
            .ToListAsync();
        var programOrder = programSlots.ToDictionary(x => x.ExerciseId, x => x.Order);
        var programIds = programOrder.Keys.ToList();

        var sets = await _db.ExerciseSets
            .Where(s => s.WorkoutSession.Date >= earliestMonthStart
                && s.SetType != SetType.Warmup
                && programIds.Contains(s.ExerciseId))
            .Select(s => new SeasonStrengthBuilder.LiftSetInput(
                s.ExerciseId, s.Exercise.Name, s.WorkoutSession.Date, s.Reps, s.WeightKg))
            .ToListAsync();

        var runs = await _db.Activities
            .Where(a => a.Type == ActivityType.Running && a.Date >= earliestMonthStart && a.DistanceKm != null)
            .Select(a => new RunLoad(a.Date, a.DistanceKm!.Value))
            .ToListAsync();

        var ultimate = await _db.Activities
            .Where(a => a.Type == ActivityType.Ultimate && a.Date >= earliestMonthStart && a.LivePlaySeconds != null)
            .Select(a => new UltimateLoad(a.Date, a.LivePlaySeconds!.Value))
            .ToListAsync();

        var tournaments = await _db.Tournaments
            .Where(t => t.EndDate >= earliestMonthStart)
            .Select(t => new SeasonStrengthBuilder.TournamentBand(t.Name, t.StartDate, t.EndDate))
            .ToListAsync();

        var seasons = await _db.Seasons
            .Where(s => s.EndDate >= earliestMonthStart)
            .Select(s => new SeasonStrengthBuilder.SeasonInput(
                s.Name, s.StartDate, s.EndDate,
                s.TargetTournament != null ? s.TargetTournament.StartDate : (DateOnly?)null))
            .ToListAsync();

        var result = SeasonStrengthBuilder.Build(today, months, sets, runs, ultimate, tournaments, seasons, programOrder);
        return Ok(result);
    }

    private static LiftSetInput ToInput(Models.ExerciseSet s) => new(s.WeightKg, s.Reps);
}
