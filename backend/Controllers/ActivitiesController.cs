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
                a.ActivitySessionTypeId, a.ActivitySessionType == null ? null : a.ActivitySessionType.Name))
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
        var activity = await _db.Activities.Include(a => a.ActivitySessionType).FirstOrDefaultAsync(a => a.Id == id);
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
            .FirstOrDefaultAsync(a => a.Id == id && a.DeletedAt != null);
        if (activity is null) return NotFound();

        activity.DeletedAt = null;
        await _db.SaveChangesAsync();

        return Ok(ToDto(activity));
    }

    private static ActivityDto ToDto(Activity a) => new(
        a.Id, a.Date, a.Type.ToString(), a.Source.ToString(), a.DurationSeconds, a.DistanceKm, a.Calories, a.AvgHeartRate, a.Notes,
        a.ActivitySessionTypeId, a.ActivitySessionType?.Name);
}
