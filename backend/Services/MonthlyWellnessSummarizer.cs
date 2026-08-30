using Callahan.Api.DTOs;

namespace Callahan.Api.Services;

// One Monday-started week of the report month: gym tonnage and that week's mean
// training-readiness, the raw material for the load-vs-recovery line.
public record WellnessWeekLoad(decimal GymVolume, double? MeanReadiness);

// The Recovery section of the monthly report: each wellness metric's month
// average against its trailing 3-month average, plus a single load-vs-recovery
// sentence. Deterministic and pure (the ReadinessInsightCalculator /
// LoadTrendBuilder pattern) so it unit-tests without a DbContext. Descriptive
// only — it reports the gap, it doesn't prescribe.
public static class MonthlyWellnessSummarizer
{
    // Non-null readings a metric needs in a window before its average is worth
    // showing. Mirrors ReadinessInsightCalculator.MinDaysPerMetric.
    private const int MinDaysPerMetric = 7;
    private const int SevenHoursSeconds = 7 * 3600;
    // Weeks with both a gym total and a readiness mean needed before the
    // load-vs-recovery line is computed at all.
    private const int MinWeeksForLoadLine = 3;
    // Readiness gap (points, month's highest-volume weeks vs trailing baseline)
    // below which the line is not worth emitting — the "in line" band from
    // ReadinessInsightCalculator's point scale.
    private const double ReadinessGapPoints = 5;

    private record Metric(string Key, string Label, Func<DailyWellnessDto, int?> Value);

    private static readonly Metric[] Metrics =
    {
        new("sleepDuration", "Sleep", w => w.SleepSeconds),
        new("sleepScore", "Sleep score", w => w.SleepScore),
        new("readiness", "Readiness", w => w.TrainingReadinessScore),
        new("hrv", "HRV", w => w.HrvLastNightAvg),
        new("restingHeartRate", "Resting HR", w => w.RestingHeartRate),
    };

    public static WellnessSectionDto? Summarize(
        IReadOnlyList<DailyWellnessDto> month,
        IReadOnlyList<DailyWellnessDto> trailing,
        int daysInMonth,
        IReadOnlyList<WellnessWeekLoad> monthWeeks,
        double? trailingReadinessAvg)
    {
        var metricDtos = new List<WellnessMetricDto>();
        var anyMetricHasEnough = false;

        foreach (var m in Metrics)
        {
            var monthVals = month.Select(m.Value).Where(v => v is not null).Select(v => (double)v!.Value).ToList();
            var trailVals = trailing.Select(m.Value).Where(v => v is not null).Select(v => (double)v!.Value).ToList();

            double? monthAvg = monthVals.Count >= MinDaysPerMetric ? monthVals.Average() : null;
            double? trailAvg = trailVals.Count >= MinDaysPerMetric ? trailVals.Average() : null;
            if (monthAvg is not null) anyMetricHasEnough = true;

            var direction = monthAvg is not null && trailAvg is not null
                ? ReadinessInsightCalculator.CompareToBaseline(m.Key, monthAvg.Value, trailAvg.Value)
                : "insufficient";

            metricDtos.Add(new WellnessMetricDto(
                m.Key, m.Label,
                monthAvg is null ? null : Math.Round((decimal)monthAvg.Value),
                trailAvg is null ? null : Math.Round((decimal)trailAvg.Value),
                direction));
        }

        // Nothing logged worth a section — let the caller omit it entirely.
        if (!anyMetricHasEnough) return null;

        var nightsLogged = month.Count(w => w.SleepSeconds is not null);
        var nightsUnder7h = month.Count(w => w.SleepSeconds is int s && s < SevenHoursSeconds);

        return new WellnessSectionDto(
            metricDtos, nightsLogged, daysInMonth, nightsUnder7h,
            BuildLoadVsRecoveryLine(monthWeeks, trailingReadinessAvg));
    }

    // "Readiness averaged N pts below your 3-month baseline across your two
    // highest-volume weeks." — only when the gap clears the in-line band.
    private static string? BuildLoadVsRecoveryLine(
        IReadOnlyList<WellnessWeekLoad> monthWeeks, double? trailingReadinessAvg)
    {
        if (trailingReadinessAvg is null) return null;

        var usable = monthWeeks
            .Where(w => w.GymVolume > 0 && w.MeanReadiness is not null)
            .OrderByDescending(w => w.GymVolume)
            .ToList();
        if (usable.Count < MinWeeksForLoadLine) return null;

        var topTwo = usable.Take(2).ToList();
        var topReadiness = topTwo.Average(w => w.MeanReadiness!.Value);
        var gap = trailingReadinessAvg.Value - topReadiness;
        if (gap <= ReadinessGapPoints) return null;

        return $"Readiness averaged {Math.Round(gap)} pts below your 3-month baseline "
             + "across your two highest-volume weeks.";
    }
}
