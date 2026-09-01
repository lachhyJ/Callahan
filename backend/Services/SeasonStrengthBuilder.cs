using Callahan.Api.DTOs;

namespace Callahan.Api.Services;

// Builds the "strength through the season" overlay: each qualifying lift's
// monthly best e1RM as a percent change from its own baseline month, plus
// monthly run / Ultimate load and the season / tournament bands to draw behind
// it. Deterministic and pure (no DbContext) - the LoadTrendBuilder pattern.
public static class SeasonStrengthBuilder
{
    public record LiftSetInput(int ExerciseId, string ExerciseName, DateOnly Date, int Reps, decimal WeightKg);
    public record TournamentBand(string Name, DateOnly Start, DateOnly End);
    public record SeasonInput(string Name, DateOnly Start, DateOnly End, DateOnly? TargetDate);

    // A lift needs at least this many distinct months of data in the window to
    // get a line - one month can't show a trajectory. Matches
    // TrendsController.GetLiftTrends.
    private const int MinMonths = 2;

    // Program slots at or above this position (1-based) are the top-of-session
    // compounds - shown by default. Deeper slots (isolation, high-rep,
    // plyometrics) are still returned but flagged non-primary so the chart
    // starts with them hidden.
    private const int PrimaryOrderMax = 2;

    public static SeasonStrengthDto Build(
        DateOnly today,
        int months,
        IEnumerable<LiftSetInput> sets,
        IEnumerable<RunLoad> runs,
        IEnumerable<UltimateLoad> ultimate,
        IEnumerable<TournamentBand> tournaments,
        IEnumerable<SeasonInput> seasons,
        IReadOnlyDictionary<int, int> programOrder)
    {
        var currentMonthStart = new DateOnly(today.Year, today.Month, 1);
        var earliestMonthStart = currentMonthStart.AddMonths(-(months - 1));
        var windowEnd = currentMonthStart.AddMonths(1).AddDays(-1);

        var monthStarts = Enumerable.Range(0, months)
            .Select(i => earliestMonthStart.AddMonths(i))
            .ToList();
        var inWindow = new HashSet<DateOnly>(monthStarts);

        // --- lift trajectories (program lifts only, ordered by program slot) ---
        var series = new List<ExerciseTrajectoryDto>();
        var byExercise = sets
            .Where(s => inWindow.Contains(MonthOf(s.Date)) && programOrder.ContainsKey(s.ExerciseId))
            .GroupBy(s => s.ExerciseId);

        foreach (var g in byExercise)
        {
            var maxByMonth = g
                .GroupBy(s => MonthOf(s.Date))
                .ToDictionary(mg => mg.Key, mg => mg.Max(s => LiftMath.Epley1Rm(s.Reps, s.WeightKg)));
            if (maxByMonth.Count < MinMonths) continue;

            var orderedMonths = maxByMonth.Keys.OrderBy(m => m).ToList();
            var baseline = maxByMonth[orderedMonths[0]];
            if (baseline <= 0) continue;

            var points = orderedMonths
                .Select(m => new TrajectoryPointDto(m, maxByMonth[m], (maxByMonth[m] - baseline) / baseline * 100m))
                .ToList();
            var isPrimary = programOrder[g.Key] <= PrimaryOrderMax;
            series.Add(new ExerciseTrajectoryDto(g.Key, g.First().ExerciseName, baseline, isPrimary, points));
        }

        series = series
            .OrderBy(s => programOrder[s.ExerciseId])
            .ThenBy(s => s.ExerciseName)
            .ToList();

        // --- monthly load ---
        var runByMonth = monthStarts.ToDictionary(m => m, _ => 0m);
        var ultSecByMonth = monthStarts.ToDictionary(m => m, _ => 0);
        foreach (var r in runs)
        {
            var m = MonthOf(r.Date);
            if (inWindow.Contains(m)) runByMonth[m] += r.DistanceKm;
        }
        foreach (var u in ultimate)
        {
            var m = MonthOf(u.Date);
            if (inWindow.Contains(m)) ultSecByMonth[m] += u.LivePlaySeconds;
        }

        var monthDtos = monthStarts
            .Select(m => new SeasonMonthDto(m, runByMonth[m], (int)Math.Round(ultSecByMonth[m] / 60.0)))
            .ToList();

        // --- bands, clipped to the window ---
        var seasonDtos = seasons
            .Where(s => s.End >= earliestMonthStart && s.Start <= windowEnd)
            .Select(s => new SeasonBandDto(
                s.Name,
                Later(s.Start, earliestMonthStart),
                Earlier(s.End, windowEnd),
                s.TargetDate is DateOnly td && td >= earliestMonthStart && td <= windowEnd ? td : null))
            .OrderBy(s => s.Start)
            .ToList();

        var bandDtos = tournaments
            .Where(t => t.End >= earliestMonthStart && t.Start <= windowEnd)
            .Select(t => new TournamentBandDto(t.Name, Later(t.Start, earliestMonthStart), Earlier(t.End, windowEnd)))
            .OrderBy(t => t.Start)
            .ToList();

        return new SeasonStrengthDto(monthDtos, series, seasonDtos, bandDtos);
    }

    private static DateOnly MonthOf(DateOnly d) => new(d.Year, d.Month, 1);
    private static DateOnly Later(DateOnly a, DateOnly b) => a > b ? a : b;
    private static DateOnly Earlier(DateOnly a, DateOnly b) => a < b ? a : b;
}
