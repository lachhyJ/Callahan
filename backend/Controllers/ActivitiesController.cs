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

    [HttpGet]
    public async Task<ActionResult<List<ActivityDto>>> GetAll()
    {
        var activities = await _db.Activities
            .OrderByDescending(a => a.Date)
            .Select(a => new ActivityDto(a.Id, a.Date, a.Type.ToString(), a.Source.ToString(), a.DurationSeconds, a.DistanceKm, a.Calories, a.AvgHeartRate, a.Notes))
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

    private static ActivityDto ToDto(Activity a) => new(
        a.Id, a.Date, a.Type.ToString(), a.Source.ToString(), a.DurationSeconds, a.DistanceKm, a.Calories, a.AvgHeartRate, a.Notes);
}
