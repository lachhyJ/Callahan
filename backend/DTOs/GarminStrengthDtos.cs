namespace Callahan.Api.DTOs;

// Posted by scripts/garmin-sync/garmin_sync.py for a Garmin strength-training
// activity - shaped like CreateActivityRequest's subset that applies to a
// lift (no distance/GPS) rather than going through /api/activities, since
// this data enriches an existing WorkoutSession instead of creating its own
// row.
public record SyncGarminStrengthRequest(
    string GarminActivityId,
    DateOnly Date,
    DateTime? StartedAt,
    int DurationSeconds,
    int? Calories,
    int? AvgHeartRate,
    decimal? ActivityTrainingLoad,
    decimal? AerobicTrainingEffect,
    decimal? AnaerobicTrainingEffect,
    string? TrainingEffectLabel,
    string? Notes,
    string? RawJson);

// Result of a sync POST: which WorkoutSession it landed on, or that it's
// sitting in the pending-review list because no single session matched.
public record GarminStrengthSyncResultDto(bool Matched, int? WorkoutSessionId, int? PendingId);

public record GarminStrengthCandidateDto(int SessionId, string? Name, DateTime? StartedAt, DateTime? FinishedAt, string? CategorySummary);

public record PendingGarminStrengthDto(
    int Id,
    string GarminActivityId,
    DateOnly Date,
    DateTime? StartedAt,
    int DurationSeconds,
    int? Calories,
    int? AvgHeartRate,
    string? Notes,
    List<GarminStrengthCandidateDto> Candidates);
