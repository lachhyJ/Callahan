namespace Callahan.Api.Models;

public class WorkoutSession
{
    public int Id { get; set; }
    public DateOnly Date { get; set; }
    public string? Name { get; set; }
    public string? Notes { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? FinishedAt { get; set; }
    public DateTime? DeletedAt { get; set; }

    public int? WorkoutTemplateId { get; set; }
    public WorkoutTemplate? WorkoutTemplate { get; set; }

    public ICollection<ExerciseSet> Sets { get; set; } = [];

    // Set once a Garmin strength-training activity is matched to this session
    // (auto, by time-window overlap, or manually via the pending-review list —
    // see GarminStrengthMatcher). Mirrors the subset of Activity's own Garmin
    // fields that make sense for a lifting session: no distance/GPS, but the
    // same Firstbeat training-load metrics. Null on every manually-logged
    // session and on anything predating this feature.
    public string? GarminActivityId { get; set; }
    public int? GarminDurationSeconds { get; set; }
    public int? GarminCalories { get; set; }
    public int? GarminAvgHeartRate { get; set; }
    public decimal? GarminActivityTrainingLoad { get; set; }
    public decimal? GarminAerobicTrainingEffect { get; set; }
    public decimal? GarminAnaerobicTrainingEffect { get; set; }
    public string? GarminTrainingEffectLabel { get; set; }
    public string? GarminRawJson { get; set; }
}
