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
    private readonly TimeProvider _time;

    public WellnessController(AppDbContext db, TimeProvider? time = null)
    {
        _db = db;
        _time = time ?? TimeProvider.System;
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
        var cutoff = _time.Today().AddDays(-LatestWindowDays);
        var wellness = await _db.DailyWellness
            .Where(w => w.Date >= cutoff)
            .Where(HasReadableMetric)
            .OrderByDescending(w => w.Date)
            .Select(WellnessMapping.Projection)
            .FirstOrDefaultAsync();

        if (wellness is null) return NoContent();
        return Ok(wellness);
    }

    [HttpGet]
    public async Task<ActionResult<List<DailyWellnessDto>>> GetAll(DateOnly? start = null, DateOnly? end = null)
    {
        var query = _db.DailyWellness.AsQueryable();
        if (start is not null) query = query.Where(w => w.Date >= start);
        if (end is not null) query = query.Where(w => w.Date <= end);

        return Ok(await query.OrderBy(w => w.Date).Select(WellnessMapping.Projection).ToListAsync());
    }

    // Phase 5: the latest day read against a trailing personal baseline, as
    // plain-language strings. Anchored to the latest row's date (not "today")
    // so a day-stale card still gets a correct comparison.
    [HttpGet("insight")]
    public async Task<ActionResult<ReadinessInsightDto>> GetInsight()
    {
        var cutoff = _time.Today().AddDays(-LatestWindowDays);
        var latest = await _db.DailyWellness
            .Where(w => w.Date >= cutoff)
            .Where(HasReadableMetric)
            .OrderByDescending(w => w.Date)
            .Select(WellnessMapping.Projection)
            .FirstOrDefaultAsync();

        if (latest is null) return NoContent();

        var windowStart = latest.Date.AddDays(-BaselineWindowDays);
        var baseline = await _db.DailyWellness
            .Where(w => w.Date >= windowStart && w.Date < latest.Date)
            .OrderBy(w => w.Date)
            .Select(WellnessMapping.Projection)
            .ToListAsync();

        var insight = ReadinessInsightCalculator.Compute(latest, baseline);
        return Ok(insight);
    }

    // Phase 5 visualisations, slice 3: weekly training load aligned with that
    // week's mean readiness / HRV / sleep score, tournament weeks flagged.
    [HttpGet("load-trend")]
    public async Task<ActionResult<List<LoadTrendWeekDto>>> GetLoadTrend([FromQuery] int weeks = 12)
    {
        weeks = Math.Clamp(weeks, 1, 52);
        var today = _time.Today();
        var earliest = CalendarDates.MondayOf(today).AddDays(-7 * (weeks - 1));

        var gymSets = await _db.ExerciseSets
            .WorkingSets()
            .Where(s => s.WorkoutSession.Date >= earliest && s.DurationSeconds == null)
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
            .Select(WellnessMapping.Projection)
            .ToListAsync();

        var tournaments = await _db.Tournaments
            .Where(t => t.EndDate >= earliest)
            .Select(t => new TournamentSpan(t.StartDate, t.EndDate))
            .ToListAsync();

        var gymGarminLoads = await _db.WorkoutSessions
            .Where(s => s.Date >= earliest && s.GarminActivityTrainingLoad != null)
            .Select(s => new GymGarminLoad(s.Date, s.GarminActivityTrainingLoad!.Value))
            .ToListAsync();

        var result = LoadTrendBuilder.Build(
            today, weeks, gymSets, runs, ultimate, wellness, tournaments, gymGarminLoads);
        return Ok(result);
    }

    [HttpPut]
    public async Task<ActionResult<DailyWellnessDto>> Upsert(UpsertDailyWellnessRequest request)
    {
        if (request.Date > _time.Today())
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
        existing.TrainingStatusCode = request.TrainingStatusCode;
        existing.TrainingStatusPhrase = request.TrainingStatusPhrase;
        existing.AcuteLoad = request.AcuteLoad;
        existing.ChronicLoad = request.ChronicLoad;
        existing.AcwrRatio = request.AcwrRatio;
        existing.Vo2Max = request.Vo2Max;
        existing.RawJson = request.RawJson;

        await _db.SaveChangesAsync();
        return Ok(ToDto(existing));
    }

    // Backfill path for the Training Status columns only. Writes just the fields
    // the request carries (null = leave alone), so a status-only backfill can
    // never null out a day's sleep / HRV / readiness the way a full PUT would.
    // A request with nothing to write is a no-op and creates no row.
    [HttpPatch("training-status")]
    public async Task<ActionResult<DailyWellnessDto>> PatchTrainingStatus(PatchTrainingStatusRequest request)
    {
        if (request.Date > _time.Today())
        {
            return BadRequest(new { error = "Date cannot be in the future." });
        }

        if (request.TrainingStatusCode is null && request.TrainingStatusPhrase is null
            && request.AcuteLoad is null && request.ChronicLoad is null
            && request.AcwrRatio is null && request.Vo2Max is null)
        {
            return NoContent();
        }

        var existing = await _db.DailyWellness.FirstOrDefaultAsync(w => w.Date == request.Date);
        if (existing is null)
        {
            existing = new DailyWellness { Date = request.Date, CreatedAt = DateTime.UtcNow };
            _db.DailyWellness.Add(existing);
        }
        else
        {
            existing.UpdatedAt = DateTime.UtcNow;
        }

        if (request.TrainingStatusCode is not null) existing.TrainingStatusCode = request.TrainingStatusCode;
        if (request.TrainingStatusPhrase is not null) existing.TrainingStatusPhrase = request.TrainingStatusPhrase;
        if (request.AcuteLoad is not null) existing.AcuteLoad = request.AcuteLoad;
        if (request.ChronicLoad is not null) existing.ChronicLoad = request.ChronicLoad;
        if (request.AcwrRatio is not null) existing.AcwrRatio = request.AcwrRatio;
        if (request.Vo2Max is not null) existing.Vo2Max = request.Vo2Max;

        await _db.SaveChangesAsync();
        return Ok(ToDto(existing));
    }

    private static DailyWellnessDto ToDto(DailyWellness w) => WellnessMapping.ToDto(w);
}
