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

    // Garmin Training Status (Productive / Maintaining / ...). The numeric code
    // is the stable key (1 Detraining, 2 Unproductive, 3 Overreaching,
    // 4 Maintaining, 5 Recovery, 6 Peaking, 7 Productive, 8 Strained); the
    // phrase is kept raw ("PRODUCTIVE_1") because its numeric suffix is
    // undocumented.
    public int? TrainingStatusCode { get; set; }
    public string? TrainingStatusPhrase { get; set; }
    // Acute (~7d) and chronic (~28d) training load and their ratio, as Garmin
    // reports them alongside the status.
    public int? AcuteLoad { get; set; }
    public int? ChronicLoad { get; set; }
    public double? AcwrRatio { get; set; }
    public double? Vo2Max { get; set; }

    // Garmin Training Load Balance: trailing-window (about 4 weeks, undocumented)
    // load split by kind, each against Garmin's own target range for that day.
    // Refreshed daily and returned per date, so it lives per day like the status
    // above. The phrase ("AEROBIC_HIGH_SHORTAGE", "ANAEROBIC_FOCUS") is kept raw:
    // a shortage outranks "focus" in it, so it can't be derived from the bars.
    public double? BalanceAerobicLow { get; set; }
    public double? BalanceAerobicHigh { get; set; }
    public double? BalanceAnaerobic { get; set; }
    public int? BalanceAerobicLowMin { get; set; }
    public int? BalanceAerobicLowMax { get; set; }
    public int? BalanceAerobicHighMin { get; set; }
    public int? BalanceAerobicHighMax { get; set; }
    public int? BalanceAnaerobicMin { get; set; }
    public int? BalanceAnaerobicMax { get; set; }
    public string? BalancePhrase { get; set; }

    // Merged raw Garmin payloads keyed by probe method name (get_sleep_data,
    // get_hrv_data, get_training_readiness, get_stats, get_training_status) - never queried, kept
    // so a field not modelled above is still recoverable from already-synced
    // rows without re-hitting Garmin.
    public string? RawJson { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
