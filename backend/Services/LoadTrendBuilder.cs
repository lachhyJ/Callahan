using Callahan.Api.DTOs;

namespace Callahan.Api.Services;

// Plain per-item load inputs, so the builder can be unit-tested without a
// DbContext (the ReadinessInsightCalculator / WeeklyConsistencyService pattern).
public record GymSetLoad(DateOnly Date, decimal Volume);
public record RunLoad(DateOnly Date, decimal DistanceKm);
public record UltimateLoad(DateOnly Date, int LivePlaySeconds);
public record TournamentSpan(DateOnly Start, DateOnly End);

// Weekly training load (gym volume, run km, Ultimate live-play) aligned with
// that week's mean readiness / HRV / sleep score, plus a tournament-week flag —
// the raw material for "does recovery track load?". Deterministic and pure;
// descriptive only, it draws no conclusions.
public static class LoadTrendBuilder
{
    // Monday-first week start, matching the frontend's convention (dateUtils.js)
    // and the copies in WorkoutSessionsController / WeeklyConsistencyService.
    public static DateOnly MondayOf(DateOnly date)
    {
        var offsetFromMonday = ((int)date.DayOfWeek + 6) % 7; // Mon=0 ... Sun=6
        return date.AddDays(-offsetFromMonday);
    }

    public static List<LoadTrendWeekDto> Build(
        DateOnly today,
        int weeks,
        IEnumerable<GymSetLoad> gymSets,
        IEnumerable<RunLoad> runs,
        IEnumerable<UltimateLoad> ultimate,
        IEnumerable<DailyWellnessDto> wellness,
        IEnumerable<TournamentSpan> tournaments)
    {
        var currentWeekStart = MondayOf(today);
        var earliest = currentWeekStart.AddDays(-7 * (weeks - 1));
        var weekStarts = Enumerable.Range(0, weeks).Select(i => earliest.AddDays(7 * i)).ToList();
        var inWindow = new HashSet<DateOnly>(weekStarts);

        var gymByWeek = weekStarts.ToDictionary(w => w, _ => 0m);
        var runByWeek = weekStarts.ToDictionary(w => w, _ => 0m);
        var ultByWeek = weekStarts.ToDictionary(w => w, _ => 0);

        foreach (var g in gymSets)
        {
            var w = MondayOf(g.Date);
            if (inWindow.Contains(w)) gymByWeek[w] += g.Volume;
        }
        foreach (var r in runs)
        {
            var w = MondayOf(r.Date);
            if (inWindow.Contains(w)) runByWeek[w] += r.DistanceKm;
        }
        foreach (var u in ultimate)
        {
            var w = MondayOf(u.Date);
            if (inWindow.Contains(w)) ultByWeek[w] += u.LivePlaySeconds;
        }

        var readiness = new Dictionary<DateOnly, (double Sum, int N)>();
        var hrv = new Dictionary<DateOnly, (double Sum, int N)>();
        var sleep = new Dictionary<DateOnly, (double Sum, int N)>();
        foreach (var d in wellness)
        {
            var w = MondayOf(d.Date);
            if (!inWindow.Contains(w)) continue;
            if (d.TrainingReadinessScore is int rs) Accumulate(readiness, w, rs);
            if (d.HrvLastNightAvg is int h) Accumulate(hrv, w, h);
            if (d.SleepScore is int ss) Accumulate(sleep, w, ss);
        }

        var tournamentWeeks = new HashSet<DateOnly>();
        foreach (var t in tournaments)
        {
            for (var d = t.Start; d <= t.End; d = d.AddDays(1))
            {
                var w = MondayOf(d);
                if (inWindow.Contains(w)) tournamentWeeks.Add(w);
            }
        }

        return weekStarts.Select(w => new LoadTrendWeekDto(
            w,
            gymByWeek[w],
            Math.Round(runByWeek[w], 2),
            (int)Math.Round(ultByWeek[w] / 60.0),
            Mean(readiness, w),
            Mean(hrv, w),
            Mean(sleep, w),
            tournamentWeeks.Contains(w))).ToList();
    }

    private static void Accumulate(Dictionary<DateOnly, (double Sum, int N)> acc, DateOnly week, double value)
    {
        var cur = acc.TryGetValue(week, out var v) ? v : (0.0, 0);
        acc[week] = (cur.Item1 + value, cur.Item2 + 1);
    }

    // Null (not zero) for a week with no readings, so the client breaks the line
    // rather than plotting a phantom drop to the axis.
    private static double? Mean(Dictionary<DateOnly, (double Sum, int N)> acc, DateOnly week) =>
        acc.TryGetValue(week, out var v) && v.N > 0 ? Math.Round(v.Sum / v.N, 1) : null;
}
