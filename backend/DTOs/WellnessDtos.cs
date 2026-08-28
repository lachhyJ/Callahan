namespace Callahan.Api.DTOs;

public record DailyWellnessDto(
    int Id,
    DateOnly Date,
    int? SleepSeconds,
    int? DeepSleepSeconds,
    int? LightSleepSeconds,
    int? RemSleepSeconds,
    int? AwakeSeconds,
    int? SleepScore,
    string? SleepScoreQualifier,
    int? HrvLastNightAvg,
    int? HrvWeeklyAvg,
    string? HrvStatus,
    int? TrainingReadinessScore,
    string? TrainingReadinessLevel,
    string? TrainingReadinessFeedback,
    int? RestingHeartRate,
    int? BodyBatteryHigh,
    int? BodyBatteryLow,
    int? AvgStressLevel);

// All metric fields default to null so the sync can post a partial payload
// (e.g. a watch that doesn't report training readiness) without the binder
// rejecting it. Null in a field means "Garmin has no value for this date" -
// PUT /api/wellness overwrites with null rather than ignoring it, so a
// retracted score stops being claimed.
// Phase 5 readiness insight: today's wellness read against a trailing personal
// baseline, delivered as finished plain-language strings (see
// ReadinessInsightCalculator). The client only renders these.
public record ReadinessInsightDto(
    DateOnly Date,
    bool HasEnoughHistory,
    string Headline,
    IReadOnlyList<MetricInsightDto> Metrics);

public record MetricInsightDto(
    string Key,           // "readiness" | "sleepScore" | "sleepDuration" | "hrv"
    string Label,         // "Readiness"
    double? Today,        // raw units: points, sleep seconds, or HRV ms
    double? BaselineAvg,  // same units, rounded; null when there is no history at all
    int BaselineDays,     // non-null days that fed BaselineAvg
    string Direction,     // "below" | "in_line" | "above" | "insufficient"
    string Phrase);       // "well below your recent average"

// One Monday-started week of training load alongside that week's mean wellness,
// for the "recovery vs load" chart (see LoadTrendBuilder). Mean* is null for a
// week with no readings.
public record LoadTrendWeekDto(
    DateOnly WeekStart,
    decimal GymVolume,          // Σ weight × reps, all sets
    decimal RunKm,
    int UltimateLivePlayMin,
    double? MeanReadiness,
    double? MeanHrv,
    double? MeanSleepScore,
    bool IsTournamentWeek);

public record UpsertDailyWellnessRequest(
    DateOnly Date,
    int? SleepSeconds = null,
    int? DeepSleepSeconds = null,
    int? LightSleepSeconds = null,
    int? RemSleepSeconds = null,
    int? AwakeSeconds = null,
    int? SleepScore = null,
    string? SleepScoreQualifier = null,
    int? HrvLastNightAvg = null,
    int? HrvWeeklyAvg = null,
    string? HrvStatus = null,
    int? TrainingReadinessScore = null,
    string? TrainingReadinessLevel = null,
    string? TrainingReadinessFeedback = null,
    int? RestingHeartRate = null,
    int? BodyBatteryHigh = null,
    int? BodyBatteryLow = null,
    int? AvgStressLevel = null,
    string? RawJson = null);
