using System.Linq.Expressions;
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
public class WellnessController : ControllerBase
{
    private readonly AppDbContext _db;

    public WellnessController(AppDbContext db)
    {
        _db = db;
    }

    // "Latest within a window" rather than "today" - a missed cron run
    // should degrade to yesterday's numbers on the dashboard card, not to
    // an empty one.
    private const int LatestWindowDays = 3;

    // Trailing window the readiness insight compares the latest day against.
    private const int BaselineWindowDays = 28;

    // Garmin creates today's row before the overnight sync has computed sleep /
    // readiness / HRV, so the newest row is often all-null. Skip those when
    // picking "latest" (and when anchoring the baseline) so the card falls back
    // to the last day that actually has numbers instead of rendering nothing.
    private static readonly Expression<Func<DailyWellness, bool>> HasReadableMetric =
        w => w.SleepSeconds != null || w.SleepScore != null
          || w.HrvLastNightAvg != null || w.TrainingReadinessScore != null;

    [HttpGet("latest")]
    public async Task<ActionResult<DailyWellnessDto>> GetLatest()
    {
        var cutoff = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-LatestWindowDays));
        var wellness = await _db.DailyWellness
            .Where(w => w.Date >= cutoff)
            .Where(HasReadableMetric)
            .OrderByDescending(w => w.Date)
            .FirstOrDefaultAsync();

        if (wellness is null) return NoContent();
        return Ok(ToDto(wellness));
    }

    [HttpGet]
    public async Task<ActionResult<List<DailyWellnessDto>>> GetAll(DateOnly? start = null, DateOnly? end = null)
    {
        var query = _db.DailyWellness.AsQueryable();
        if (start is not null) query = query.Where(w => w.Date >= start);
        if (end is not null) query = query.Where(w => w.Date <= end);

        var wellness = await query.OrderBy(w => w.Date).ToListAsync();
        return Ok(wellness.Select(ToDto).ToList());
    }

    // Phase 5: the latest day read against a trailing personal baseline, as
    // plain-language strings. Anchored to the latest row's date (not "today")
    // so a day-stale card still gets a correct comparison.
    [HttpGet("insight")]
    public async Task<ActionResult<ReadinessInsightDto>> GetInsight()
    {
        var cutoff = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-LatestWindowDays));
        var latest = await _db.DailyWellness
            .Where(w => w.Date >= cutoff)
            .Where(HasReadableMetric)
            .OrderByDescending(w => w.Date)
            .FirstOrDefaultAsync();

        if (latest is null) return NoContent();

        var windowStart = latest.Date.AddDays(-BaselineWindowDays);
        var baseline = await _db.DailyWellness
            .Where(w => w.Date >= windowStart && w.Date < latest.Date)
            .OrderBy(w => w.Date)
            .ToListAsync();

        var insight = ReadinessInsightCalculator.Compute(
            ToDto(latest),
            baseline.Select(ToDto).ToList());
        return Ok(insight);
    }

    // Phase 5 visualisations, slice 3: weekly training load aligned with that
    // week's mean readiness / HRV / sleep score, tournament weeks flagged.
    [HttpGet("load-trend")]
    public async Task<ActionResult<List<LoadTrendWeekDto>>> GetLoadTrend([FromQuery] int weeks = 12)
    {
        weeks = Math.Clamp(weeks, 1, 52);
        var today = DateOnly.FromDateTime(DateTime.Now);
        var earliest = LoadTrendBuilder.MondayOf(today).AddDays(-7 * (weeks - 1));

        var gymSets = await _db.ExerciseSets
            .Where(s => s.WorkoutSession.Date >= earliest)
            .Select(s => new GymSetLoad(s.WorkoutSession.Date, s.WeightKg * s.Reps))
            .ToListAsync();

        var runs = await _db.Activities
            .Where(a => a.Type == ActivityType.Running && a.Date >= earliest && a.DistanceKm != null)
            .Select(a => new RunLoad(a.Date, a.DistanceKm!.Value))
            .ToListAsync();

        var ultimate = await _db.Activities
            .Where(a => a.Type == ActivityType.Ultimate && a.Date >= earliest && a.LivePlaySeconds != null)
            .Select(a => new UltimateLoad(a.Date, a.LivePlaySeconds!.Value))
            .ToListAsync();

        var wellness = await _db.DailyWellness
            .Where(w => w.Date >= earliest)
            .OrderBy(w => w.Date)
            .ToListAsync();

        var tournaments = await _db.Tournaments
            .Where(t => t.EndDate >= earliest)
            .Select(t => new TournamentSpan(t.StartDate, t.EndDate))
            .ToListAsync();

        var result = LoadTrendBuilder.Build(
            today, weeks, gymSets, runs, ultimate, wellness.Select(ToDto), tournaments);
        return Ok(result);
    }

    [HttpPut]
    public async Task<ActionResult<DailyWellnessDto>> Upsert(UpsertDailyWellnessRequest request)
    {
        if (request.Date > DateOnly.FromDateTime(DateTime.UtcNow))
        {
            return BadRequest(new { error = "Date cannot be in the future." });
        }

        var existing = await _db.DailyWellness.FirstOrDefaultAsync(w => w.Date == request.Date);
        if (existing is null)
        {
            existing = new DailyWellness
            {
                Date = request.Date,
                CreatedAt = DateTime.UtcNow,
            };
            _db.DailyWellness.Add(existing);
        }
        else
        {
            existing.UpdatedAt = DateTime.UtcNow;
        }

        existing.SleepSeconds = request.SleepSeconds;
        existing.DeepSleepSeconds = request.DeepSleepSeconds;
        existing.LightSleepSeconds = request.LightSleepSeconds;
        existing.RemSleepSeconds = request.RemSleepSeconds;
        existing.AwakeSeconds = request.AwakeSeconds;
        existing.SleepScore = request.SleepScore;
        existing.SleepScoreQualifier = request.SleepScoreQualifier;
        existing.HrvLastNightAvg = request.HrvLastNightAvg;
        existing.HrvWeeklyAvg = request.HrvWeeklyAvg;
        existing.HrvStatus = request.HrvStatus;
        existing.TrainingReadinessScore = request.TrainingReadinessScore;
        existing.TrainingReadinessLevel = request.TrainingReadinessLevel;
        existing.TrainingReadinessFeedback = request.TrainingReadinessFeedback;
        existing.RestingHeartRate = request.RestingHeartRate;
        existing.BodyBatteryHigh = request.BodyBatteryHigh;
        existing.BodyBatteryLow = request.BodyBatteryLow;
        existing.AvgStressLevel = request.AvgStressLevel;
        existing.RawJson = request.RawJson;

        await _db.SaveChangesAsync();
        return Ok(ToDto(existing));
    }

    private static DailyWellnessDto ToDto(DailyWellness w) => new(
        w.Id, w.Date,
        w.SleepSeconds, w.DeepSleepSeconds, w.LightSleepSeconds, w.RemSleepSeconds, w.AwakeSeconds,
        w.SleepScore, w.SleepScoreQualifier,
        w.HrvLastNightAvg, w.HrvWeeklyAvg, w.HrvStatus,
        w.TrainingReadinessScore, w.TrainingReadinessLevel, w.TrainingReadinessFeedback,
        w.RestingHeartRate, w.BodyBatteryHigh, w.BodyBatteryLow, w.AvgStressLevel);
}
