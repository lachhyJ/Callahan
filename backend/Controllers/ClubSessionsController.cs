using Callahan.Api.Data;
using Callahan.Api.DTOs;
using Callahan.Api.Models;
using Callahan.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Callahan.Api.Controllers;

// Club Training sessions: any Ultimate activity carrying the Club Training tag
// (primary or extra), newest first.
[ApiController]
[Authorize]
[Route("api/club-sessions")]
public class ClubSessionsController : ControllerBase
{
    private const string ClubTrainingName = "Club Training";

    private readonly AppDbContext _db;

    public ClubSessionsController(AppDbContext db)
    {
        _db = db;
    }

    private IQueryable<Activity> ClubQuery() => _db.Activities.AsNoTracking()
        .Where(a => a.Type == ActivityType.Ultimate
            && a.SessionTags.Any(t => t.SessionType.Name == ClubTrainingName));

    [HttpGet]
    public async Task<ActionResult<List<ClubSessionDto>>> GetAll()
    {
        var rows = await ClubQuery()
            .OrderByDescending(a => a.Date).ThenByDescending(a => a.StartTimeLocal)
            .Select(a => new { Activity = a, a.Track!.SamplesJson })
            .ToListAsync();
        return Ok(rows.Select(r => ToDto(r.Activity, r.SamplesJson)).ToList());
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<ClubSessionDto>> Get(int id)
    {
        var row = await ClubQuery().Where(a => a.Id == id)
            .Select(a => new { Activity = a, a.Track!.SamplesJson })
            .FirstOrDefaultAsync();
        return row is null ? NotFound() : Ok(ToDto(row.Activity, row.SamplesJson));
    }

    private static ClubSessionDto ToDto(Activity a, string? samplesJson)
    {
        var raw = ClubSessionMetrics.ParseRaw(a.RawJson);
        ClubSessionMetrics.TrackFigures? track = null;
        if (samplesJson is not null && ActivitiesController.TryDecodeTrack(samplesJson) is { Count: > 0 } samples)
            track = ClubSessionMetrics.FromTrack(samples);
        return new ClubSessionDto(
            a.Id, a.Date, a.StartTimeLocal, a.DurationSeconds, a.Notes, a.DistanceKm, a.Calories, a.AvgHeartRate,
            raw.MaxHeartRate, a.ActivityTrainingLoad, a.AerobicTrainingEffect, a.AnaerobicTrainingEffect,
            a.TrainingEffectLabel, raw.ZoneSeconds,
            track is null ? null : (decimal)track.HighSpeedDistanceM, track?.SprintCount);
    }
}
