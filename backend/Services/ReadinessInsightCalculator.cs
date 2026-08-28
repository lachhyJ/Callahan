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
    // Resting-HR bands, |delta| as a percentage of the baseline average - tighter
    // than HRV because night-to-night resting HR barely moves (a few bpm is a lot).
    private const double RhrPctInLine = 4;
    private const double RhrPctStrong = 8;

    private enum Scale { Point, SleepMinutes, HrvPercent, RhrPercent }

    // LowerIsBetter flips which numeric direction counts as the fatigue signal:
    // for resting HR an *elevated* reading is the "more tired than usual" case.
    private record MetricSpec(
        string Key, string Label, string HeadlineNoun, Scale Scale,
        Func<DailyWellnessDto, double?> Value, bool LowerIsBetter = false);

    private static readonly MetricSpec[] Specs =
    {
        new("readiness", "Readiness", "readiness", Scale.Point, w => w.TrainingReadinessScore),
        new("sleepScore", "Sleep score", "sleep", Scale.Point, w => w.SleepScore),
        new("sleepDuration", "Sleep duration", "sleep", Scale.SleepMinutes, w => w.SleepSeconds),
        new("hrv", "HRV", "HRV", Scale.HrvPercent, w => w.HrvLastNightAvg),
        new("restingHeartRate", "Resting HR", "resting HR", Scale.RhrPercent, w => w.RestingHeartRate, LowerIsBetter: true),
    };

    // baselineRows: wellness rows strictly before today.Date, window already
    // pre-filtered by the caller. Sparse rows (nulls) are fine - each metric
    // averages only the days it actually has.
    public static ReadinessInsightDto Compute(DailyWellnessDto today, IReadOnlyList<DailyWellnessDto> baselineRows)
    {
        var metrics = new List<MetricInsightDto>();
        // Recovery is the fatigue-oriented reading: "bad" = worse than baseline
        // (low readiness/sleep/HRV, or high resting HR), "good" = the opposite.
        var notable = new List<(string Noun, string Recovery, int Strength)>();

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
            var (numericDir, strength, phrase) = Band(spec.Scale, todayValue.Value, avg);

            string recovery = numericDir switch
            {
                "in_line" => "in_line",
                "below" => spec.LowerIsBetter ? "good" : "bad",
                _ => spec.LowerIsBetter ? "bad" : "good",   // "above"
            };
            // DTO direction carries recovery semantics so the client tints an
            // elevated resting HR as a negative, not a positive.
            string dtoDirection = recovery switch { "bad" => "below", "good" => "above", _ => "in_line" };

            metrics.Add(new MetricInsightDto(
                spec.Key, spec.Label,
                Math.Round(todayValue.Value), Math.Round(avg),
                count, dtoDirection, phrase));

            if (recovery is "good" or "bad")
                notable.Add((spec.HeadlineNoun, recovery, strength));
        }

        bool hasEnoughHistory = metrics.Any(m => m.Direction != "insufficient");
        return new ReadinessInsightDto(today.Date, hasEnoughHistory, BuildHeadline(hasEnoughHistory, notable), metrics);
    }

    private static (string NumericDir, int Strength, string Phrase) Band(Scale scale, double today, double avg)
    {
        double delta = scale switch
        {
            Scale.SleepMinutes => (today - avg) / 60.0,                       // seconds -> minutes
            Scale.HrvPercent or Scale.RhrPercent => avg == 0 ? 0 : (today - avg) / avg * 100.0,
            _ => today - avg,                                                 // raw points
        };
        var (inLine, strong) = scale switch
        {
            Scale.SleepMinutes => (SleepMinInLine, SleepMinStrong),
            Scale.HrvPercent => (HrvPctInLine, HrvPctStrong),
            Scale.RhrPercent => (RhrPctInLine, RhrPctStrong),
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

    private static string BuildHeadline(bool hasEnoughHistory, List<(string Noun, string Recovery, int Strength)> notable)
    {
        if (!hasEnoughHistory) return "Not enough wellness history yet.";
        if (notable.Count == 0) return "You're tracking close to your recent baseline.";

        var bad = notable.Where(n => n.Recovery == "bad").OrderByDescending(n => n.Strength).ToList();
        var good = notable.Where(n => n.Recovery == "good").OrderByDescending(n => n.Strength).ToList();

        var badNouns = DistinctNouns(bad);
        var goodNouns = DistinctNouns(good);

        if (bad.Count > 0 && good.Count > 0)
            return Capitalize($"{JoinNouns(badNouns)} lagging, {JoinNouns(goodNouns)} ahead — a mixed picture.");

        if (bad.Count > 0)
        {
            string verb = badNouns.Count == 1 ? "is" : "are";
            string tail = bad.Any(b => b.Strength == 2) ? " — more tired than usual." : ".";
            return Capitalize($"{JoinNouns(badNouns)} {verb} lagging your recent baseline{tail}");
        }

        string verbG = goodNouns.Count == 1 ? "is" : "are";
        string tailG = good.Any(g => g.Strength == 2) ? " — fresher than usual." : ".";
        return Capitalize($"{JoinNouns(goodNouns)} {verbG} ahead of your recent baseline{tailG}");
    }

    private static List<string> DistinctNouns(List<(string Noun, string Recovery, int Strength)> items)
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
