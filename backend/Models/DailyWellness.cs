namespace Callahan.Api.Models;

public class DailyWellness
{
    public int Id { get; set; }
    public DateOnly Date { get; set; }

    public int? SleepSeconds { get; set; }
    public int? DeepSleepSeconds { get; set; }
    public int? LightSleepSeconds { get; set; }
    public int? RemSleepSeconds { get; set; }
    public int? AwakeSeconds { get; set; }
    public int? SleepScore { get; set; }
    public string? SleepScoreQualifier { get; set; }

    public int? HrvLastNightAvg { get; set; }
    public int? HrvWeeklyAvg { get; set; }
    public string? HrvStatus { get; set; }

    public int? TrainingReadinessScore { get; set; }
    public string? TrainingReadinessLevel { get; set; }
    public string? TrainingReadinessFeedback { get; set; }

    public int? RestingHeartRate { get; set; }
    public int? BodyBatteryHigh { get; set; }
    public int? BodyBatteryLow { get; set; }
    public int? AvgStressLevel { get; set; }

    // Merged raw Garmin payloads keyed by probe method name (get_sleep_data,
    // get_hrv_data, get_training_readiness, get_stats) - never queried, kept
    // so a field not modelled above is still recoverable from already-synced
    // rows without re-hitting Garmin.
    public string? RawJson { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
