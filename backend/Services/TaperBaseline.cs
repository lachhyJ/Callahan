using Callahan.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Callahan.Api.Services;

// The gym-volume half of the taper maths, shared by the live recommendation
// (TaperController) and the monthly report's planned-vs-actual comparison, so
// "your usual week" means the same thing in both. The step percentages
// themselves live in TaperPhaseCalculator.
//
// Volume here counts every set with a load, warmups included - unlike the
// Trends volume chart, which excludes them. Whether it should is an open
// question (the audit's BUG-4); this only keeps the two taper copies agreeing.
public static class TaperBaseline
{
    // The baseline is the four weeks immediately before the taper window opens.
    public const int BaselineDays = 28;

    public static DateOnly BaselineStart(DateOnly taperStart) => taperStart.AddDays(-BaselineDays);

    // Sum of weight x reps over [from, toExclusive), timed holds excluded.
    // Summed in memory: SQLite can't aggregate decimals server-side.
    public static async Task<decimal> GymVolumeAsync(AppDbContext db, DateOnly from, DateOnly toExclusive)
    {
        var sets = await db.ExerciseSets
            .Where(s => s.WorkoutSession.Date >= from && s.WorkoutSession.Date < toExclusive && s.DurationSeconds == null)
            .Select(s => new { s.WeightKg, s.Reps })
            .ToListAsync();
        return sets.Sum(s => s.WeightKg * s.Reps);
    }

    public static async Task<decimal> WeeklyGymBaselineAsync(AppDbContext db, DateOnly taperStart) =>
        await GymVolumeAsync(db, BaselineStart(taperStart), taperStart) / (BaselineDays / 7m);
}
