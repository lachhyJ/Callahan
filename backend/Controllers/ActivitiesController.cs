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
public class ActivitiesController : ControllerBase
{
    private readonly AppDbContext _db;

    public ActivitiesController(AppDbContext db)
    {
        _db = db;
    }

    private const int RecoveryWindowDays = 7;

    private async Task PurgeExpiredAsync()
    {
        var cutoff = DateTime.UtcNow.AddDays(-RecoveryWindowDays);
        var expired = await _db.Activities.IgnoreQueryFilters()
            .Where(a => a.DeletedAt != null && a.DeletedAt < cutoff)
            .ToListAsync();
        if (expired.Count == 0) return;

        _db.Activities.RemoveRange(expired);
        await _db.SaveChangesAsync();
    }

    [HttpGet]
    public async Task<ActionResult<List<ActivityDto>>> GetAll(DateOnly? start = null, DateOnly? end = null)
    {
        await PurgeExpiredAsync();

        var query = _db.Activities.AsQueryable();
        if (start is not null) query = query.Where(a => a.Date >= start);
        if (end is not null) query = query.Where(a => a.Date <= end);

        var activities = await query
            .Include(a => a.ActivitySessionType)
            .OrderByDescending(a => a.Date)
            .Select(a => new ActivityDto(
                a.Id, a.Date, a.Type.ToString(), a.Source.ToString(), a.DurationSeconds, a.DistanceKm, a.Calories, a.AvgHeartRate, a.Notes,
                a.ActivitySessionTypeId, a.ActivitySessionType == null ? null : a.ActivitySessionType.Name,
                a.Laps.Count, a.HighSpeedDistanceM == null ? null : a.HighSpeedDistanceM / 1000, a.ConeDistanceM))
            .ToListAsync();

        return Ok(activities);
    }

    [HttpPost]
    public async Task<ActionResult<ActivityDto>> Create(CreateActivityRequest request)
    {
        if (!Enum.TryParse<ActivityType>(request.Type, ignoreCase: true, out var type))
        {
            return BadRequest(new { error = $"Unknown activity type '{request.Type}'." });
        }
        if (!Enum.TryParse<ActivitySource>(request.Source, ignoreCase: true, out var source))
        {
            return BadRequest(new { error = $"Unknown activity source '{request.Source}'." });
        }

        // Re-syncing the same Garmin activity should be idempotent, not create duplicates -
        // but should still pick up edits made in Garmin Connect after the first sync (e.g.
        // a renamed title, which lands in Notes). ActivitySessionTypeId is Callahan's own
        // classification and is deliberately left untouched on re-sync.
        if (request.GarminActivityId is not null)
        {
            var existing = await _db.Activities
                .Include(a => a.ActivitySessionType)
                .Include(a => a.Laps)
                .FirstOrDefaultAsync(a => a.GarminActivityId == request.GarminActivityId);
            if (existing is not null)
            {
                existing.Date = request.Date;
                existing.Type = type;
                existing.Source = source;
                existing.DurationSeconds = request.DurationSeconds;
                existing.DistanceKm = request.DistanceKm;
                existing.Calories = request.Calories;
                existing.AvgHeartRate = request.AvgHeartRate;
                existing.Notes = request.Notes;
                await _db.SaveChangesAsync();
                return Ok(ToDto(existing));
            }
        }

        var activity = new Activity
        {
            Date = request.Date,
            Type = type,
            Source = source,
            DurationSeconds = request.DurationSeconds,
            DistanceKm = request.DistanceKm,
            Calories = request.Calories,
            AvgHeartRate = request.AvgHeartRate,
            Notes = request.Notes,
            GarminActivityId = request.GarminActivityId
        };

        _db.Activities.Add(activity);
        await _db.SaveChangesAsync();

        return Ok(ToDto(activity));
    }

    [HttpPut("{id}/session-type")]
    public async Task<ActionResult<ActivityDto>> UpdateSessionType(int id, UpdateActivitySessionTypeRequest request)
    {
        var activity = await _db.Activities.Include(a => a.ActivitySessionType).Include(a => a.Laps).FirstOrDefaultAsync(a => a.Id == id);
        if (activity is null) return NotFound();

        if (request.ActivitySessionTypeId is not null)
        {
            var sessionType = await _db.ActivitySessionTypes.FirstOrDefaultAsync(t => t.Id == request.ActivitySessionTypeId);
            if (sessionType is null)
            {
                return BadRequest(new { error = $"Unknown activity session type '{request.ActivitySessionTypeId}'." });
            }
            if (sessionType.ActivityType != activity.Type)
            {
                return BadRequest(new { error = $"'{sessionType.Name}' is a {sessionType.ActivityType} session type, not valid for a {activity.Type} activity." });
            }
        }

        activity.ActivitySessionTypeId = request.ActivitySessionTypeId;
        await _db.SaveChangesAsync();

        // Re-fetch the nav property now that the FK changed.
        await _db.Entry(activity).Reference(a => a.ActivitySessionType).LoadAsync();

        return Ok(ToDto(activity));
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var activity = await _db.Activities.FirstOrDefaultAsync(a => a.Id == id);
        if (activity is null) return NotFound();

        activity.DeletedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return NoContent();
    }

    [HttpGet("deleted")]
    public async Task<ActionResult<List<DeletedActivityDto>>> GetDeleted()
    {
        await PurgeExpiredAsync();

        var activities = await _db.Activities.IgnoreQueryFilters()
            .Where(a => a.DeletedAt != null)
            .Include(a => a.ActivitySessionType)
            .OrderByDescending(a => a.DeletedAt)
            .Select(a => new DeletedActivityDto(
                a.Id, a.Date, a.Type.ToString(), a.Source.ToString(), a.DurationSeconds, a.DistanceKm, a.Notes,
                a.ActivitySessionTypeId, a.ActivitySessionType == null ? null : a.ActivitySessionType.Name,
                a.DeletedAt!.Value))
            .ToListAsync();

        return Ok(activities);
    }

    [HttpPost("{id}/restore")]
    public async Task<ActionResult<ActivityDto>> Restore(int id)
    {
        var activity = await _db.Activities.IgnoreQueryFilters()
            .Include(a => a.ActivitySessionType)
            .Include(a => a.Laps)
            .FirstOrDefaultAsync(a => a.Id == id && a.DeletedAt != null);
        if (activity is null) return NotFound();

        activity.DeletedAt = null;
        await _db.SaveChangesAsync();

        return Ok(ToDto(activity));
    }

    // High-speed laps are exactly what Garmin's own IntensityType already
    // marks "ACTIVE" for a structured interval workout - no speed-threshold
    // heuristic needed, confirmed against a real High Speed Intervals
    // session via --dump-laps (2026-08-25).
    private const string HighSpeedIntensityType = "ACTIVE";

    [HttpPut("{id}/laps")]
    public async Task<ActionResult<ActivityLapsResponse>> ReplaceLaps(int id, UpsertActivityLapsRequest request)
    {
        var activity = await _db.Activities.Include(a => a.Laps).FirstOrDefaultAsync(a => a.Id == id);
        if (activity is null) return NotFound();

        // Laps are immutable per activity once Garmin has recorded them, so
        // a re-sync just replaces the whole set rather than trying to diff -
        // simpler and idempotent regardless of how many times it's called.
        _db.ActivityLaps.RemoveRange(activity.Laps);

        var laps = request.Laps.Select(l => new ActivityLap
        {
            ActivityId = id,
            LapIndex = l.LapIndex,
            IntensityType = l.IntensityType,
            DistanceM = l.DistanceM,
            DurationSeconds = l.DurationSeconds,
            MovingDurationSeconds = l.MovingDurationSeconds,
            AvgSpeedMps = l.AvgSpeedMps,
            MaxSpeedMps = l.MaxSpeedMps,
            AvgHeartRate = l.AvgHeartRate,
            MaxHeartRate = l.MaxHeartRate,
        }).ToList();

        _db.ActivityLaps.AddRange(laps);

        var activeLaps = laps.Where(l => l.IntensityType == HighSpeedIntensityType).ToList();
        activity.HighSpeedDistanceM = activeLaps.Count > 0 ? activeLaps.Sum(l => l.DistanceM ?? 0) : null;

        await _db.SaveChangesAsync();

        return Ok(new ActivityLapsResponse(
            laps.OrderBy(l => l.LapIndex).Select(ToLapDto).ToList(),
            activity.HighSpeedDistanceM == null ? null : activity.HighSpeedDistanceM / 1000));
    }

    [HttpGet("{id}/laps")]
    public async Task<ActionResult<ActivityLapsResponse>> GetLaps(int id)
    {
        var activity = await _db.Activities.Include(a => a.Laps).FirstOrDefaultAsync(a => a.Id == id);
        if (activity is null) return NotFound();

        return Ok(new ActivityLapsResponse(
            activity.Laps.OrderBy(l => l.LapIndex).Select(ToLapDto).ToList(),
            activity.HighSpeedDistanceM == null ? null : activity.HighSpeedDistanceM / 1000));
    }

    [HttpPut("{id}/cone-distance")]
    public async Task<ActionResult<ActivityDto>> UpdateConeDistance(int id, UpdateConeDistanceRequest request)
    {
        var activity = await _db.Activities.Include(a => a.ActivitySessionType).Include(a => a.Laps).FirstOrDefaultAsync(a => a.Id == id);
        if (activity is null) return NotFound();

        activity.ConeDistanceM = request.ConeDistanceM;
        await _db.SaveChangesAsync();

        return Ok(ToDto(activity));
    }

    private static ActivityLapDto ToLapDto(ActivityLap l) => new(
        l.LapIndex, l.IntensityType, l.DistanceM, l.DurationSeconds, l.MovingDurationSeconds,
        l.AvgSpeedMps, l.MaxSpeedMps, l.AvgHeartRate, l.MaxHeartRate);

    private static ActivityDto ToDto(Activity a) => new(
        a.Id, a.Date, a.Type.ToString(), a.Source.ToString(), a.DurationSeconds, a.DistanceKm, a.Calories, a.AvgHeartRate, a.Notes,
        a.ActivitySessionTypeId, a.ActivitySessionType?.Name,
        a.Laps.Count, a.HighSpeedDistanceM == null ? null : a.HighSpeedDistanceM / 1000, a.ConeDistanceM);
}
