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
