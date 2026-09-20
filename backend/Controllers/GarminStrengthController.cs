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
[Route("api/garmin-strength")]
public class GarminStrengthController : ControllerBase
{
    private readonly AppDbContext _db;

    public GarminStrengthController(AppDbContext db)
    {
        _db = db;
    }

    // Called by scripts/garmin-sync/garmin_sync.py for each Garmin
    // strength-training activity in the sync window. Idempotent on
    // GarminActivityId same as /api/activities: a re-sync of an already
    // matched or already pending activity just refreshes its stored fields,
    // never creates a duplicate or re-prompts for a link that's already made.
    [HttpPost]
    public async Task<ActionResult<GarminStrengthSyncResultDto>> Sync(SyncGarminStrengthRequest request)
    {
        var existingSession = await _db.WorkoutSessions
            .FirstOrDefaultAsync(s => s.GarminActivityId == request.GarminActivityId);
        if (existingSession is not null)
        {
            GarminStrengthMatcher.Apply(existingSession, request.GarminActivityId, request.DurationSeconds,
                request.Calories, request.AvgHeartRate, request.ActivityTrainingLoad,
                request.AerobicTrainingEffect, request.AnaerobicTrainingEffect, request.TrainingEffectLabel,
                request.RawJson ?? existingSession.GarminRawJson);
            await _db.SaveChangesAsync();
            return Ok(new GarminStrengthSyncResultDto(true, existingSession.Id, null));
        }

        var existingPending = await _db.PendingGarminStrengthActivities
            .FirstOrDefaultAsync(p => p.GarminActivityId == request.GarminActivityId);

        var candidates = await GarminStrengthMatcher.FindCandidatesAsync(_db, request.Date, request.StartedAt);

        if (candidates.Count == 1)
        {
            var session = candidates[0];
            GarminStrengthMatcher.Apply(session, request.GarminActivityId, request.DurationSeconds,
                request.Calories, request.AvgHeartRate, request.ActivityTrainingLoad,
                request.AerobicTrainingEffect, request.AnaerobicTrainingEffect, request.TrainingEffectLabel,
                request.RawJson);
            if (existingPending is not null) _db.Remove(existingPending);
            await _db.SaveChangesAsync();
            return Ok(new GarminStrengthSyncResultDto(true, session.Id, null));
        }

        // Zero or multiple same-day unlinked sessions overlap the window -
        // can't pick one automatically. Upsert the pending row (a re-sync
        // before it's resolved just refreshes the stored metrics) for the
        // review list to surface.
        var pending = existingPending ?? new PendingGarminStrengthActivity
        {
            GarminActivityId = request.GarminActivityId,
            CreatedAt = DateTime.UtcNow,
        };
        pending.Date = request.Date;
        pending.StartedAt = request.StartedAt;
        pending.DurationSeconds = request.DurationSeconds;
        pending.Calories = request.Calories;
        pending.AvgHeartRate = request.AvgHeartRate;
        pending.ActivityTrainingLoad = request.ActivityTrainingLoad;
        pending.AerobicTrainingEffect = request.AerobicTrainingEffect;
        pending.AnaerobicTrainingEffect = request.AnaerobicTrainingEffect;
        pending.TrainingEffectLabel = request.TrainingEffectLabel;
        pending.Notes = request.Notes;
        pending.RawJson = request.RawJson;

        if (existingPending is null) _db.Add(pending);
        await _db.SaveChangesAsync();

        return Ok(new GarminStrengthSyncResultDto(false, null, pending.Id));
    }

    // Candidates are recomputed on read, not stored at sync time - a session
    // logged after the Garmin sync ran (the normal order: lift, then Garmin
    // syncs overnight, but manual entry can happen anytime) should still show
    // up as pickable.
    [HttpGet("pending")]
    public async Task<ActionResult<List<PendingGarminStrengthDto>>> GetPending()
    {
        var pendingList = await _db.PendingGarminStrengthActivities
            .OrderByDescending(p => p.Date)
            .ToListAsync();

        var result = new List<PendingGarminStrengthDto>();
        foreach (var p in pendingList)
        {
            var candidates = await GarminStrengthMatcher.FindCandidatesAsync(_db, p.Date, p.StartedAt);
            result.Add(new PendingGarminStrengthDto(
                p.Id, p.GarminActivityId, p.Date, p.StartedAt, p.DurationSeconds, p.Calories, p.AvgHeartRate,
                p.Notes,
                candidates.Select(c => new GarminStrengthCandidateDto(
                    c.Id, c.Name, c.StartedAt, c.FinishedAt, WorkoutSessionsController.CategorySummary(c.Sets))).ToList()));
        }

        return Ok(result);
    }

    [HttpPost("pending/{pendingId}/link/{sessionId}")]
    public async Task<IActionResult> Link(int pendingId, int sessionId)
    {
        var pending = await _db.PendingGarminStrengthActivities.FindAsync(pendingId);
        if (pending is null) return NotFound();

        var session = await _db.WorkoutSessions.FindAsync(sessionId);
        if (session is null) return NotFound(new { error = "Workout session not found." });
        if (session.GarminActivityId is not null)
        {
            return BadRequest(new { error = "That session is already linked to a different Garmin activity." });
        }

        GarminStrengthMatcher.Apply(session, pending.GarminActivityId, pending.DurationSeconds,
            pending.Calories, pending.AvgHeartRate, pending.ActivityTrainingLoad,
            pending.AerobicTrainingEffect, pending.AnaerobicTrainingEffect, pending.TrainingEffectLabel,
            pending.RawJson);

        _db.Remove(pending);
        await _db.SaveChangesAsync();

        return NoContent();
    }

    // For a Garmin strength activity that isn't actually a Callahan gym
    // session (e.g. a one-off hotel-gym session never logged here) - drops it
    // from the review list with no link made. Nothing marks the
    // GarminActivityId as "seen and rejected", so a re-sync within the
    // lookback window (--days, default 14) will re-create it as pending -
    // acceptable since dismissing something that recent and re-dismissing it
    // once or twice is cheap, and the alternative (a permanent ignore-list)
    // isn't worth the extra table for how rarely this fires.
    [HttpDelete("pending/{pendingId}")]
    public async Task<IActionResult> Dismiss(int pendingId)
    {
        var pending = await _db.PendingGarminStrengthActivities.FindAsync(pendingId);
        if (pending is null) return NotFound();

        _db.Remove(pending);
        await _db.SaveChangesAsync();

        return NoContent();
    }
}
