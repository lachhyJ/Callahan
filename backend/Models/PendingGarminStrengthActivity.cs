namespace Callahan.Api.Models;

// A Garmin strength-training activity that GarminStrengthMatcher couldn't
// confidently attach to exactly one WorkoutSession (zero or multiple
// same-day sessions overlapping its time window). Held here for manual
// resolution from the review list rather than silently dropped or guessed
// at. Resolved rows (linked or dismissed) are deleted, not soft-kept — once
// acted on there's nothing left worth keeping a record of.
public class PendingGarminStrengthActivity
{
    public int Id { get; set; }
    public string GarminActivityId { get; set; } = "";
    public DateOnly Date { get; set; }
    public DateTime? StartedAt { get; set; }
    public int DurationSeconds { get; set; }
    public int? Calories { get; set; }
    public int? AvgHeartRate { get; set; }
    public decimal? ActivityTrainingLoad { get; set; }
    public decimal? AerobicTrainingEffect { get; set; }
    public decimal? AnaerobicTrainingEffect { get; set; }
    public string? TrainingEffectLabel { get; set; }
    public string? Notes { get; set; }
    public string? RawJson { get; set; }
    public DateTime CreatedAt { get; set; }
}
