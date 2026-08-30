using Callahan.Api.DTOs;
using Callahan.Api.Models;

namespace Callahan.Api.Services;

// Single model -> DTO projection for daily wellness, shared by WellnessController
// and the monthly report builder so the field order only lives in one place.
public static class WellnessMapping
{
    public static DailyWellnessDto ToDto(DailyWellness w) => new(
        w.Id, w.Date,
        w.SleepSeconds, w.DeepSleepSeconds, w.LightSleepSeconds, w.RemSleepSeconds, w.AwakeSeconds,
        w.SleepScore, w.SleepScoreQualifier,
        w.HrvLastNightAvg, w.HrvWeeklyAvg, w.HrvStatus,
        w.TrainingReadinessScore, w.TrainingReadinessLevel, w.TrainingReadinessFeedback,
        w.RestingHeartRate, w.BodyBatteryHigh, w.BodyBatteryLow, w.AvgStressLevel);
}
