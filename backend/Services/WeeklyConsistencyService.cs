using static Callahan.Api.Services.CalendarDates;
namespace Callahan.Api.Services;

public record WeeklyConsistencyDefinition(string Type, string Label, Func<int, int, bool> Qualifies);

// The "weekly consistency" rules behind the monthly report's weekly targets. There used
// to be a streaks page on the same rules; it went in program v4 (nobody opened it, and
// the 3+ gym definitions cannot be hit in-season, when the program is two gym sessions a
// week), so only the two definitions that fit both blocks remain.
public static class WeeklyConsistencyService
{
    public static readonly WeeklyConsistencyDefinition[] Definitions =
    [
        new("gym2", "2+ gym sessions", (gym, run) => gym >= 2),
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
}
