using Callahan.Api.DTOs;

namespace Callahan.Api.Services;

// One Ultimate activity's contribution to monthly distance. DistanceKm is null
// for indoor / non-GPS / manually-logged sessions - those are counted
// separately, never estimated. Type is the primary session-type label (Game,
// Pod, Solo, Throws, or a Field type logged as an Ultimate activity). Distance
// is additive, so it's attributed wholly to the primary label - deliberately
// NOT the consistency section's count-under-every-tag rule, which would
// double-count a sum.
public record UltimateDistanceActivity(DateOnly Date, decimal? DistanceKm, string Type);

// Garmin's own per-activity training load (Firstbeat / EPOC), for one activity
// that the watch actually scored. IsUltimate splits the monthly total into its
// Ultimate share. Activities with no load (short / no-HR / manual) are simply
// absent from this list.
public record GarminLoad(DateOnly Date, decimal TrainingLoad, bool IsUltimate);

// Monthly Ultimate distance (whole-recording GPS km) with a per-session-type
// breakdown and a count of sessions that carried no GPS distance, plus run km
// and Garmin's summed training load (total and the Ultimate share) alongside.
// Deterministic and pure (no DbContext) - the LoadTrendBuilder pattern.
// Descriptive only; it draws no conclusions.
public static class UltimateDistanceBuilder
{
    public static DateOnly MonthOf(DateOnly d) => new(d.Year, d.Month, 1);

    public static List<UltimateDistanceMonthDto> Build(
        DateOnly today,
        int months,
        IEnumerable<UltimateDistanceActivity> ultimate,
        IEnumerable<RunLoad> runs,
        IEnumerable<GarminLoad> loads)
    {
        var currentMonthStart = new DateOnly(today.Year, today.Month, 1);
        var earliestMonthStart = currentMonthStart.AddMonths(-(months - 1));
        var monthStarts = Enumerable.Range(0, months)
            .Select(i => earliestMonthStart.AddMonths(i))
            .ToList();
        var inWindow = new HashSet<DateOnly>(monthStarts);

        var byMonth = monthStarts.ToDictionary(m => m, _ => new Dictionary<string, TypeAcc>());
        var runByMonth = monthStarts.ToDictionary(m => m, _ => 0m);
        var loadByMonth = monthStarts.ToDictionary(m => m, _ => new LoadAcc());

        foreach (var a in ultimate)
        {
            var m = MonthOf(a.Date);
            if (!inWindow.Contains(m)) continue;

            var types = byMonth[m];
            if (!types.TryGetValue(a.Type, out var acc))
            {
                acc = new TypeAcc();
                types[a.Type] = acc;
            }
            acc.Sessions++;
            if (a.DistanceKm is decimal km) acc.Km += km;
            else acc.WithoutDistance++;
        }

        foreach (var r in runs)
        {
            var m = MonthOf(r.Date);
            if (inWindow.Contains(m)) runByMonth[m] += r.DistanceKm;
        }

        foreach (var l in loads)
        {
            var m = MonthOf(l.Date);
            if (!inWindow.Contains(m)) continue;
            var acc = loadByMonth[m];
            acc.Total += l.TrainingLoad;
            acc.Scored++;
            if (l.IsUltimate)
            {
                acc.Ultimate += l.TrainingLoad;
                acc.UltimateScored++;
            }
        }

        return monthStarts.Select(m =>
        {
            var accs = byMonth[m];

            var byType = accs
                .Select(kv => new UltimateDistanceByTypeDto(
                    kv.Key,
                    Math.Round(kv.Value.Km, 2),
                    kv.Value.Sessions,
                    kv.Value.WithoutDistance))
                .OrderByDescending(t => t.Km)
                .ThenByDescending(t => t.Sessions)
                .ThenBy(t => t.TypeName)
                .ToList();

            // Month totals come off the raw accumulators, rounded once - not
            // from summing the already-rounded per-type figures.
            var ultimateKm = Math.Round(accs.Values.Sum(a => a.Km), 2);
            var withoutDistance = accs.Values.Sum(a => a.WithoutDistance);

            var load = loadByMonth[m];
            // Null (not 0) for a month with no scored session, so a client
            // breaks the line rather than plotting a phantom drop to zero.
            decimal? totalLoad = load.Scored > 0 ? Math.Round(load.Total, 0) : null;
            decimal? ultimateLoad = load.UltimateScored > 0 ? Math.Round(load.Ultimate, 0) : null;

            return new UltimateDistanceMonthDto(
                m, ultimateKm, Math.Round(runByMonth[m], 2), withoutDistance, byType,
                ultimateLoad, totalLoad);
        }).ToList();
    }

    private sealed class TypeAcc
    {
        public decimal Km;
        public int Sessions;
        public int WithoutDistance;
    }

    private sealed class LoadAcc
    {
        public decimal Total;
        public decimal Ultimate;
        public int Scored;
        public int UltimateScored;
    }
}
