using static Callahan.Api.Services.CalendarDates;
namespace Callahan.Api.Services;

public record WeeklyConsistencyDefinition(string Type, string Label, Func<int, int, bool> Qualifies);

// Single source of truth for the "weekly consistency" rules — a missed
// single day shouldn't reset a streak that's about training consistency,
// not attendance-taking. Originally lived only in StreaksController;
// extracted so MonthlyReportBuilder can reuse the exact same definitions
// and Monday-start week bucketing without risking drift between the two.
public static class WeeklyConsistencyService
{
    public static readonly WeeklyConsistencyDefinition[] Definitions =
    [
        new("gym2", "2+ gym sessions", (gym, run) => gym >= 2),
        new("gym3", "3+ gym sessions", (gym, run) => gym >= 3),
        new("gym3run1", "3 gym + a run", (gym, run) => gym >= 3 && run >= 1),
        new("run1", "1+ run", (gym, run) => run >= 1),
    ];


    // Per-week gym/run counts keyed by the Monday of each week, for every
    // week that has at least one workout or run in it. Callers needing a
    // dense (gap-filled) range should build one around this.
    public static Dictionary<DateOnly, (int Gym, int Run)> BucketByWeek(List<DateOnly> workoutDates, List<DateOnly> runDates)
    {
        var buckets = new Dictionary<DateOnly, (int Gym, int Run)>();
        foreach (var d in workoutDates)
        {
            var wk = MondayOf(d);
            buckets[wk] = (buckets.TryGetValue(wk, out var v) ? v.Gym + 1 : 1, buckets.TryGetValue(wk, out var v2) ? v2.Run : 0);
        }
        foreach (var d in runDates)
        {
            var wk = MondayOf(d);
            var existing = buckets.TryGetValue(wk, out var v) ? v : (0, 0);
            buckets[wk] = (existing.Item1, existing.Item2 + 1);
        }
        return buckets;
    }

    // Current and best run of qualifying weeks, oldest week first. The current
    // (in-progress) week hasn't finished, so it can't yet break a streak: when
    // it hasn't qualified (yet), the current streak is counted back from last
    // week instead of being treated as a miss.
    public static (int Current, int Best) Streak(bool[] qualifiesByWeek)
    {
        var best = 0;
        var run = 0;
        foreach (var q in qualifiesByWeek)
        {
            run = q ? run + 1 : 0;
            best = Math.Max(best, run);
        }

        var lastIndex = qualifiesByWeek.Length - 1;
        var startIndex = lastIndex >= 0 && qualifiesByWeek[lastIndex] ? lastIndex
            : lastIndex - 1 >= 0 && qualifiesByWeek[lastIndex - 1] ? lastIndex - 1
            : -1;

        var current = 0;
        for (var idx = startIndex; idx >= 0 && qualifiesByWeek[idx]; idx--) current++;
        return (current, best);
    }
}
