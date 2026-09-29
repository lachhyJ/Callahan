using Callahan.Api.Models;

namespace Callahan.Api.Services;

public static class ExerciseSetQueries
{
    // The one definition of "working" sets: everything except warmups. Every
    // volume, set-count and best-lift figure reads through this, so a warmup
    // can't inflate one chart while another leaves it out.
    public static IQueryable<ExerciseSet> WorkingSets(this IQueryable<ExerciseSet> sets) =>
        sets.Where(s => s.SetType != SetType.Warmup);
}
