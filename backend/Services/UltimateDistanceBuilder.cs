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

// Monthly Ultimate distance (whole-recording GPS km) with a per-session-type
// breakdown and a count of sessions that carried no GPS distance, plus run km
// alongside for a combined view. Deterministic and pure (no DbContext) - the
// LoadTrendBuilder pattern. Descriptive only; it draws no conclusions.
public static class UltimateDistanceBuilder
{
    public static DateOnly MonthOf(DateOnly d) => new(d.Year, d.Month, 1);

    public static List<UltimateDistanceMonthDto> Build(
        DateOnly today,
        int months,
        IEnumerable<UltimateDistanceActivity> ultimate,
        IEnumerable<RunLoad> runs)
    {
        var currentMonthStart = new DateOnly(today.Year, today.Month, 1);
        var earliestMonthStart = currentMonthStart.AddMonths(-(months - 1));
        var monthStarts = Enumerable.Range(0, months)
            .Select(i => earliestMonthStart.AddMonths(i))
            .ToList();
        var inWindow = new HashSet<DateOnly>(monthStarts);

        var byMonth = monthStarts.ToDictionary(m => m, _ => new Dictionary<string, TypeAcc>());
        var runByMonth = monthStarts.ToDictionary(m => m, _ => 0m);

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

            return new UltimateDistanceMonthDto(
                m, ultimateKm, Math.Round(runByMonth[m], 2), withoutDistance, byType);
        }).ToList();
    }

    private sealed class TypeAcc
    {
        public decimal Km;
        public int Sessions;
        public int WithoutDistance;
    }
}
