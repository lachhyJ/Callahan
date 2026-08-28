using Callahan.Api.DTOs;

namespace Callahan.Api.Services;

// Deterministic "how does today compare to my recent normal" read over the
// Garmin wellness history. Plain-language only - descriptive baseline context
// ("42 vs ~68 typical") is fine, a load prescription / percentage target is
// not (.ui-craft/brief.md). Mirrors TaperPhaseCalculator: pure, static,
// unit-tested in isolation rather than through the controller.
public static class ReadinessInsightCalculator
{
    // Tunable knobs, grouped at the top like TaperPhaseCalculator's phase cutoffs.
    private const int MinDaysPerMetric = 7;   // non-null baseline days needed before a metric is compared

    // Point-scale bands (readiness, sleep score - both 0-100), |delta| in points.
    private const double PointInLine = 5;
    private const double PointStrong = 12;
    // Sleep-duration bands, |delta| in minutes.
    private const double SleepMinInLine = 20;
    private const double SleepMinStrong = 45;
    // HRV bands, |delta| as a percentage of the baseline average.
    private const double HrvPctInLine = 8;
    private const double HrvPctStrong = 15;

    private enum Scale { Point, SleepMinutes, HrvPercent }

    private record MetricSpec(string Key, string Label, string HeadlineNoun, Scale Scale, Func<DailyWellnessDto, double?> Value);

    private static readonly MetricSpec[] Specs =
    {
        new("readiness", "Readiness", "readiness", Scale.Point, w => w.TrainingReadinessScore),
        new("sleepScore", "Sleep score", "sleep", Scale.Point, w => w.SleepScore),
        new("sleepDuration", "Sleep duration", "sleep", Scale.SleepMinutes, w => w.SleepSeconds),
        new("hrv", "HRV", "HRV", Scale.HrvPercent, w => w.HrvLastNightAvg),
    };

    // baselineRows: wellness rows strictly before today.Date, window already
    // pre-filtered by the caller. Sparse rows (nulls) are fine - each metric
    // averages only the days it actually has.
    public static ReadinessInsightDto Compute(DailyWellnessDto today, IReadOnlyList<DailyWellnessDto> baselineRows)
    {
        var metrics = new List<MetricInsightDto>();
        var notable = new List<(string Noun, string Direction, int Strength)>();

        foreach (var spec in Specs)
        {
            double? todayValue = spec.Value(today);
            var baselineValues = baselineRows
                .Select(spec.Value)
                .Where(v => v is not null)
                .Select(v => v!.Value)
                .ToList();
            int count = baselineValues.Count;

            if (count < MinDaysPerMetric || todayValue is null)
            {
                string reason = todayValue is null ? "No reading today" : "Not enough history yet";
                metrics.Add(new MetricInsightDto(
                    spec.Key, spec.Label,
                    todayValue,
                    count > 0 ? Math.Round(baselineValues.Average()) : null,
                    count, "insufficient", reason));
                continue;
            }

            double avg = baselineValues.Average();
            var (direction, strength, phrase) = Band(spec.Scale, todayValue.Value, avg);
            metrics.Add(new MetricInsightDto(
                spec.Key, spec.Label,
                Math.Round(todayValue.Value), Math.Round(avg),
                count, direction, phrase));

            if (direction is "below" or "above")
                notable.Add((spec.HeadlineNoun, direction, strength));
        }

        bool hasEnoughHistory = metrics.Any(m => m.Direction != "insufficient");
        return new ReadinessInsightDto(today.Date, hasEnoughHistory, BuildHeadline(hasEnoughHistory, notable), metrics);
    }

    private static (string Direction, int Strength, string Phrase) Band(Scale scale, double today, double avg)
    {
        double delta = scale switch
        {
            Scale.SleepMinutes => (today - avg) / 60.0,                    // seconds -> minutes
            Scale.HrvPercent => avg == 0 ? 0 : (today - avg) / avg * 100.0, // percent of baseline
            _ => today - avg,                                              // raw points
        };
        var (inLine, strong) = scale switch
        {
            Scale.SleepMinutes => (SleepMinInLine, SleepMinStrong),
            Scale.HrvPercent => (HrvPctInLine, HrvPctStrong),
            _ => (PointInLine, PointStrong),
        };

        double mag = Math.Abs(delta);
        if (mag <= inLine)
            return ("in_line", 0, "in line with your recent average");

        int strength = mag > strong ? 2 : 1;
        bool below = delta < 0;
        string phrase = (scale, below, strength) switch
        {
            (Scale.SleepMinutes, true, 2) => "well below your usual sleep",
            (Scale.SleepMinutes, true, _) => "a bit less sleep than usual",
            (Scale.SleepMinutes, false, 2) => "well above your usual sleep",
            (Scale.SleepMinutes, false, _) => "a bit more sleep than usual",
            (_, true, 2) => "well below your recent average",
            (_, true, _) => "a bit below your recent average",
            (_, false, 2) => "well above your recent average",
            (_, false, _) => "a bit above your recent average",
        };
        return (below ? "below" : "above", strength, phrase);
    }

    private static string BuildHeadline(bool hasEnoughHistory, List<(string Noun, string Direction, int Strength)> notable)
    {
        if (!hasEnoughHistory) return "Not enough wellness history yet.";
        if (notable.Count == 0) return "You're tracking in line with your recent average.";

        var belows = notable.Where(n => n.Direction == "below").OrderByDescending(n => n.Strength).ToList();
        var aboves = notable.Where(n => n.Direction == "above").OrderByDescending(n => n.Strength).ToList();

        var belowNouns = DistinctNouns(belows);
        var aboveNouns = DistinctNouns(aboves);

        if (belows.Count > 0 && aboves.Count > 0)
            return Capitalize($"{JoinNouns(belowNouns)} down, {JoinNouns(aboveNouns)} up — a mixed picture.");

        if (belows.Count > 0)
        {
            string verb = belowNouns.Count == 1 ? "is" : "are";
            string tail = belows.Any(b => b.Strength == 2) ? " — more tired than usual." : ".";
            return Capitalize($"{JoinNouns(belowNouns)} {verb} below your recent average{tail}");
        }

        string verbA = aboveNouns.Count == 1 ? "is" : "are";
        string tailA = aboves.Any(a => a.Strength == 2) ? " — fresher than usual." : ".";
        return Capitalize($"{JoinNouns(aboveNouns)} {verbA} above your recent average{tailA}");
    }

    private static List<string> DistinctNouns(List<(string Noun, string Direction, int Strength)> items)
    {
        var seen = new HashSet<string>();
        var result = new List<string>();
        foreach (var it in items)
            if (seen.Add(it.Noun))
                result.Add(it.Noun);
        return result;
    }

    private static string JoinNouns(List<string> nouns) => nouns.Count switch
    {
        0 => "",
        1 => nouns[0],
        2 => $"{nouns[0]} and {nouns[1]}",
        _ => $"{string.Join(", ", nouns.Take(nouns.Count - 1))} and {nouns[^1]}",
    };

    private static string Capitalize(string s) => string.IsNullOrEmpty(s) ? s : char.ToUpper(s[0]) + s[1..];
}
