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
            .Include(a => a.RunSessionType)
            .OrderByDescending(a => a.Date)
            .Select(a => new ActivityDto(
                a.Id, a.Date, a.Type.ToString(), a.Source.ToString(), a.DurationSeconds, a.DistanceKm, a.Calories, a.AvgHeartRate, a.Notes,
                a.RunSessionTypeId, a.RunSessionType == null ? null : a.RunSessionType.Name))
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

        // Re-syncing the same Garmin activity should be idempotent, not create duplicates.
        if (request.GarminActivityId is not null)
        {
            var existing = await _db.Activities
                .Include(a => a.RunSessionType)
                .FirstOrDefaultAsync(a => a.GarminActivityId == request.GarminActivityId);
            if (existing is not null)
            {
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

    [HttpPut("{id}/run-session-type")]
    public async Task<ActionResult<ActivityDto>> UpdateRunSessionType(int id, UpdateActivityRunSessionTypeRequest request)
    {
        var activity = await _db.Activities.Include(a => a.RunSessionType).FirstOrDefaultAsync(a => a.Id == id);
        if (activity is null) return NotFound();

        if (request.RunSessionTypeId is not null && !await _db.RunSessionTypes.AnyAsync(t => t.Id == request.RunSessionTypeId))
        {
            return BadRequest(new { error = $"Unknown run session type '{request.RunSessionTypeId}'." });
        }

        activity.RunSessionTypeId = request.RunSessionTypeId;
        await _db.SaveChangesAsync();

        // Re-fetch the nav property now that the FK changed.
        await _db.Entry(activity).Reference(a => a.RunSessionType).LoadAsync();

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
            .Include(a => a.RunSessionType)
            .OrderByDescending(a => a.DeletedAt)
            .Select(a => new DeletedActivityDto(
                a.Id, a.Date, a.Type.ToString(), a.Source.ToString(), a.DurationSeconds, a.DistanceKm, a.Notes,
                a.RunSessionTypeId, a.RunSessionType == null ? null : a.RunSessionType.Name,
                a.DeletedAt!.Value))
            .ToListAsync();

        return Ok(activities);
    }

    [HttpPost("{id}/restore")]
    public async Task<ActionResult<ActivityDto>> Restore(int id)
    {
        var activity = await _db.Activities.IgnoreQueryFilters()
            .Include(a => a.RunSessionType)
            .FirstOrDefaultAsync(a => a.Id == id && a.DeletedAt != null);
        if (activity is null) return NotFound();

        activity.DeletedAt = null;
        await _db.SaveChangesAsync();

        return Ok(ToDto(activity));
    }

    private static ActivityDto ToDto(Activity a) => new(
        a.Id, a.Date, a.Type.ToString(), a.Source.ToString(), a.DurationSeconds, a.DistanceKm, a.Calories, a.AvgHeartRate, a.Notes,
        a.RunSessionTypeId, a.RunSessionType?.Name);
}
