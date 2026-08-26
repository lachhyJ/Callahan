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
public class ActivitiesController : ControllerBase
{
    private readonly AppDbContext _db;

    public ActivitiesController(AppDbContext db)
    {
        _db = db;
    }

    private const int RecoveryWindowDays = 7;

    // The one Ultimate session type that gets on/off-field lap classification -
    // it's the only one with sub rotations. Matches the seed in AppDbContext.
    private const string GameSessionTypeName = "Game";

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
                a.Laps.Count, a.Laps.Count(l => l.IntensityType == "ACTIVE"),
                a.HighSpeedDistanceM == null ? null : a.HighSpeedDistanceM / 1000, a.ConeDistanceM,
                a.OnFieldSeconds, a.OffFieldSeconds, a.MixedSeconds, a.PointsPlayed,
                a.OnFieldDistanceM == null ? null : a.OnFieldDistanceM / 1000,
                a.AlternationViolations, a.LapClassifierMethod, a.OnFieldSpeedThresholdMps, a.LapClassifierVersion))
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
                // Null-coalesce so a manual re-POST without the field can't wipe
                // a blob a previous Garmin sync captured.
                existing.RawJson = request.RawJson ?? existing.RawJson;
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
            GarminActivityId = request.GarminActivityId,
            RawJson = request.RawJson,
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

        // Load the new nav property before classifying - the helper keys off
        // ActivitySessionType.Name.
        await _db.Entry(activity).Reference(a => a.ActivitySessionType).LoadAsync();

        // A manual re-classify can turn an activity into a Game (compute the
        // on/off-field split from its laps) or back out of one (clear it).
        ApplyLapDerivedAggregates(activity, activity.Laps.ToList());

        await _db.SaveChangesAsync();

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
        var activity = await _db.Activities
            .Include(a => a.ActivitySessionType)
            .Include(a => a.Laps)
            .FirstOrDefaultAsync(a => a.Id == id);
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

        ApplyLapDerivedAggregates(activity, laps);

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

    // Re-run LapFieldClassifier over every Ultimate Game activity that has
    // laps, in place, with no Garmin traffic - raw laps are already stored.
    // This is what makes shipping provisional v1 thresholds safe: retuning is
    // a constant change in LapClassifierOptions, a Version bump, and one POST.
    // force=true reclassifies every Game; otherwise only those on an older
    // classifier version (or never classified).
    [HttpPost("laps/reclassify")]
    public async Task<ActionResult<ReclassifyResponse>> ReclassifyLaps(bool force = false)
    {
        var candidates = await _db.Activities
            .Include(a => a.ActivitySessionType)
            .Include(a => a.Laps)
            .Where(a => a.Type == ActivityType.Ultimate
                && a.ActivitySessionType != null
                && a.ActivitySessionType.Name == GameSessionTypeName
                && a.Laps.Any()
                && (force
                    || a.LapClassifierVersion == null
                    || a.LapClassifierVersion < LapFieldClassifier.Version))
            .OrderBy(a => a.Date)
            .ToListAsync();

        var changes = new List<ReclassifyChange>();
        foreach (var activity in candidates)
        {
            var before = activity.LapClassifierMethod;
            ApplyLapDerivedAggregates(activity, activity.Laps.ToList());
            changes.Add(new ReclassifyChange(
                activity.Id, activity.Date, before, activity.LapClassifierMethod,
                activity.PointsPlayed, activity.AlternationViolations));
        }

        await _db.SaveChangesAsync();

        return Ok(new ReclassifyResponse(LapFieldClassifier.Version, changes.Count, changes));
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

    // Recompute an activity's lap-derived columns from its laps. Shared by
    // ReplaceLaps (new laps), UpdateSessionType (a manual re-classify can move
    // an activity into or out of "Game"), and ReclassifyLaps (bulk) so the
    // three can't drift. Mutates the passed laps' FieldState and the activity's
    // cached columns; the caller saves. Requires activity.ActivitySessionType
    // to be loaded.
    private static void ApplyLapDerivedAggregates(Activity activity, List<ActivityLap> laps)
    {
        // Running: high-speed distance is Garmin's own ACTIVE labelling. The
        // field-state columns don't apply.
        if (activity.Type == ActivityType.Running)
        {
            var activeLaps = laps.Where(l => l.IntensityType == HighSpeedIntensityType).ToList();
            activity.HighSpeedDistanceM = activeLaps.Count > 0 ? activeLaps.Sum(l => l.DistanceM ?? 0) : null;
            return;
        }

        // Ultimate "Game": classify each lap on/off-field.
        if (activity.Type == ActivityType.Ultimate
            && activity.ActivitySessionType?.Name == GameSessionTypeName)
        {
            var summary = LapFieldClassifier.Classify(laps);
            foreach (var lap in laps)
            {
                lap.FieldState = summary.StateByLapIndex.TryGetValue(lap.LapIndex, out var s)
                    ? s : LapFieldState.Unknown;
            }
            activity.OnFieldSeconds = summary.OnFieldSeconds;
            activity.OffFieldSeconds = summary.OffFieldSeconds;
            activity.MixedSeconds = summary.MixedSeconds;
            activity.PointsPlayed = summary.PointsPlayed;
            activity.OnFieldDistanceM = summary.OnFieldDistanceM;
            activity.AlternationViolations = summary.AlternationViolations;
            activity.LapClassifierMethod = summary.Method;
            activity.OnFieldSpeedThresholdMps = summary.ThresholdMps;
            activity.LapClassifierVersion = LapFieldClassifier.Version;
            return;
        }

        // Any other Ultimate session type, or unclassified: clear the columns
        // so an activity re-classified away from Game doesn't strand stale
        // numbers.
        foreach (var lap in laps) lap.FieldState = null;
        activity.OnFieldSeconds = null;
        activity.OffFieldSeconds = null;
        activity.MixedSeconds = null;
        activity.PointsPlayed = null;
        activity.OnFieldDistanceM = null;
        activity.AlternationViolations = null;
        activity.LapClassifierMethod = null;
        activity.OnFieldSpeedThresholdMps = null;
        activity.LapClassifierVersion = null;
    }

    private static ActivityLapDto ToLapDto(ActivityLap l) => new(
        l.LapIndex, l.IntensityType, l.DistanceM, l.DurationSeconds, l.MovingDurationSeconds,
        l.AvgSpeedMps, l.MaxSpeedMps, l.AvgHeartRate, l.MaxHeartRate, l.FieldState);

    private static ActivityDto ToDto(Activity a) => new(
        a.Id, a.Date, a.Type.ToString(), a.Source.ToString(), a.DurationSeconds, a.DistanceKm, a.Calories, a.AvgHeartRate, a.Notes,
        a.ActivitySessionTypeId, a.ActivitySessionType?.Name,
        a.Laps.Count, a.Laps.Count(l => l.IntensityType == "ACTIVE"),
        a.HighSpeedDistanceM == null ? null : a.HighSpeedDistanceM / 1000, a.ConeDistanceM,
        a.OnFieldSeconds, a.OffFieldSeconds, a.MixedSeconds, a.PointsPlayed,
        a.OnFieldDistanceM == null ? null : a.OnFieldDistanceM / 1000,
        a.AlternationViolations, a.LapClassifierMethod, a.OnFieldSpeedThresholdMps, a.LapClassifierVersion);
}
