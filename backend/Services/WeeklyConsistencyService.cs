using Callahan.Api.Data;
using Callahan.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Callahan.Api.Services;

public record WeeklyConsistencyDefinition(string Type, string Label, Func<int, int, bool> Qualifies);

// Single source of truth for the "weekly consistency" rules — a missed
// single day shouldn't reset a streak that's about training consistency,
// not attendance-taking. Originally lived only in StreaksController;
// extracted so MonthlyReportBuilder can reuse the exact same definitions
// and Monday-start week bucketing without risking drift between the two.
public class WeeklyConsistencyService
{
    private readonly AppDbContext _db;

    public WeeklyConsistencyService(AppDbContext db)
    {
        _db = db;
    }

    public static readonly WeeklyConsistencyDefinition[] Definitions =
    [
        new("gym2", "2+ gym sessions", (gym, run) => gym >= 2),
        new("total3", "3+ sessions", (gym, run) => gym + run >= 3),
        new("gym3run1", "3 gym + a run", (gym, run) => gym >= 3 && run >= 1),
        new("run1", "1+ run", (gym, run) => run >= 1),
    ];

    // Monday-first week start, matching the frontend's convention (dateUtils.js).
    public static DateOnly MondayOf(DateOnly date)
    {
        var offsetFromMonday = ((int)date.DayOfWeek + 6) % 7; // Mon=0 ... Sun=6
        return date.AddDays(-offsetFromMonday);
    }

    public async Task<(List<DateOnly> WorkoutDates, List<DateOnly> RunDates)> GetSessionDatesAsync()
    {
        var workoutDates = await _db.WorkoutSessions.Select(s => s.Date).ToListAsync();
        var runDates = await _db.Activities.Where(a => a.Type == ActivityType.Running).Select(a => a.Date).ToListAsync();
        return (workoutDates, runDates);
    }

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
