using System.Linq.Expressions;
using Callahan.Api.DTOs;
using Callahan.Api.Models;

namespace Callahan.Api.Services;

// Single model -> DTO projection for daily wellness, shared by WellnessController
// and the monthly report builder so the field order only lives in one place.
//
// Reads should use Projection inside the query (.Select(WellnessMapping.Projection))
// so SQLite returns only these columns. Each row also carries RawJson, Garmin's
// full sleep/HRV payload - ~136 KB a day on average in production - which
// materialising the entity loads and then throws away.
public static class WellnessMapping
{
    public static readonly Expression<Func<DailyWellness, DailyWellnessDto>> Projection = w => new DailyWellnessDto(
        w.Id, w.Date,
        w.SleepSeconds, w.DeepSleepSeconds, w.LightSleepSeconds, w.RemSleepSeconds, w.AwakeSeconds,
        w.SleepScore, w.SleepScoreQualifier,
        w.HrvLastNightAvg, w.HrvWeeklyAvg, w.HrvStatus,
        w.TrainingReadinessScore, w.TrainingReadinessLevel, w.TrainingReadinessFeedback,
        w.RestingHeartRate, w.BodyBatteryHigh, w.BodyBatteryLow, w.AvgStressLevel);

    private static readonly Func<DailyWellness, DailyWellnessDto> Compiled = Projection.Compile();

    // For an entity already in memory (the upsert's response).
    public static DailyWellnessDto ToDto(DailyWellness w) => Compiled(w);
}
