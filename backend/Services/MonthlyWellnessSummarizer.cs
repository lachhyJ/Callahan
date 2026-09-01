using Callahan.Api.DTOs;

namespace Callahan.Api.Services;

// The Recovery section of the monthly report: each wellness metric's month
// average against its trailing 3-month average. Deterministic and pure (the
// ReadinessInsightCalculator / LoadTrendBuilder pattern) so it unit-tests
// without a DbContext. Descriptive only — it reports the gap, it doesn't
// prescribe.
//
// Training readiness is deliberately absent. It's an acute, strongly
// mean-reverting daily score whose expected value tracks yesterday's load, so
// a month average compared to a 3-month baseline says almost nothing - and
// the load-vs-recovery line this used to emit ("readiness averaged N pts
// below baseline across your two highest-volume weeks") restated a tautology
// as an insight. Readiness stays where it's genuinely useful: same-day and
// prospective, on the dashboard. What belongs in a monthly retrospective is
// the slow-moving stuff, so resting HR and HRV lead.
public static class MonthlyWellnessSummarizer
{
    // Non-null readings a metric needs in a window before its average is worth
    // showing. Mirrors ReadinessInsightCalculator.MinDaysPerMetric.
    private const int MinDaysPerMetric = 7;
    private const int SevenHoursSeconds = 7 * 3600;

    private record Metric(string Key, string Label, Func<DailyWellnessDto, int?> Value);

    private static readonly Metric[] Metrics =
    {
        new("restingHeartRate", "Resting HR", w => w.RestingHeartRate),
        new("hrv", "HRV", w => w.HrvLastNightAvg),
        new("sleepDuration", "Sleep", w => w.SleepSeconds),
        new("sleepScore", "Sleep score", w => w.SleepScore),
    };

    public static WellnessSectionDto? Summarize(
        IReadOnlyList<DailyWellnessDto> month,
        IReadOnlyList<DailyWellnessDto> trailing,
        int daysInMonth)
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

        return new WellnessSectionDto(metricDtos, nightsLogged, daysInMonth, nightsUnder7h);
    }
}
