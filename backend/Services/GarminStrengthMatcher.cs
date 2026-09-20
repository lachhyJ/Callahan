using Callahan.Api.Data;
using Callahan.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Callahan.Api.Services;

// Matches a Garmin strength-training activity to the WorkoutSession it
// belongs to, so a synced activity enriches Callahan's own manually-logged
// gym session rather than living as a disconnected record. Time-window
// overlap only - there's no other signal to go on (Garmin doesn't know about
// exercises/sets, and Callahan's sessions don't carry a Garmin-comparable
// GPS/HR stream to cross-check against).
public static class GarminStrengthMatcher
{
    // How far a WorkoutSession's StartedAt may sit from the Garmin activity's
    // start and still count as "the same session" - covers a watch started
    // a few minutes early/late, or stopped after standing around packing up.
    // Wide enough for normal slop, narrow enough that two real sessions on
    // the same day won't both fall inside it.
    private static readonly TimeSpan ToleranceWindow = TimeSpan.FromMinutes(45);

    public record Candidate(int SessionId, string? Name, DateTime? StartedAt, DateTime? FinishedAt, string? CategorySummary);

    // Sessions on the Garmin activity's date, not already linked to a
    // different Garmin activity, whose StartedAt falls within
    // ToleranceWindow of the activity's start (or - for a session logged
    // without a StartedAt, e.g. an old Hevy import - any unlinked session on
    // that date, since there's no time to compare against).
    public static async Task<List<WorkoutSession>> FindCandidatesAsync(
        AppDbContext db, DateOnly date, DateTime? startedAt)
    {
        var sameDay = await db.WorkoutSessions
            .Where(s => s.Date == date && s.GarminActivityId == null)
            .Include(s => s.Sets).ThenInclude(set => set.Exercise)
            .ToListAsync();

        if (startedAt is null) return sameDay;

        return sameDay
            .Where(s => s.StartedAt is null || (s.StartedAt.Value - startedAt.Value).Duration() <= ToleranceWindow)
            .ToList();
    }

    public static void Apply(WorkoutSession session, string garminActivityId, int durationSeconds,
        int? calories, int? avgHeartRate, decimal? trainingLoad, decimal? aerobicEffect,
        decimal? anaerobicEffect, string? trainingEffectLabel, string? rawJson)
    {
        session.GarminActivityId = garminActivityId;
        session.GarminDurationSeconds = durationSeconds;
        session.GarminCalories = calories;
        session.GarminAvgHeartRate = avgHeartRate;
        session.GarminActivityTrainingLoad = trainingLoad;
        session.GarminAerobicTrainingEffect = aerobicEffect;
        session.GarminAnaerobicTrainingEffect = anaerobicEffect;
        session.GarminTrainingEffectLabel = trainingEffectLabel;
        session.GarminRawJson = rawJson;
    }
}
