namespace Callahan.Api.Models;

public class ExerciseSet
{
    public int Id { get; set; }
    public int WorkoutSessionId { get; set; }
    public WorkoutSession WorkoutSession { get; set; } = null!;

    public int ExerciseId { get; set; }
    public Exercise Exercise { get; set; } = null!;

    public int Reps { get; set; }

    // Negative on assisted exercises (the magnitude is the assistance), zero
    // at bodyweight, positive for external load. See LiftProgress for why that
    // ordering matters and why e1RM can't be used below zero.
    //
    // WARNING - never compare or order this SERVER-SIDE. EF stores decimal as
    // TEXT on SQLite, so the column has TEXT affinity and comparisons are
    // lexicographic, not numeric: on real data `WeightKg > 9` matches 21 rows
    // where `CAST(WeightKg AS REAL) > 9` matches 1211, because '10' <= '9' as
    // text. Zero is also stored as both '0' and '0.0', which don't compare
    // equal. Arithmetic (*, SUM) is fine - SQLite coerces for those - and
    // every comparison in this codebase runs in memory after ToListAsync,
    // which is why nothing is currently wrong. Keep it that way: materialise
    // first, or CAST explicitly in raw SQL. Applies to all 12 decimal columns
    // (Activities.DistanceKm, ActivityLaps.*, Tournaments.*, ...).
    public decimal WeightKg { get; set; }
    public int SetOrder { get; set; }
    public SetType SetType { get; set; } = SetType.Normal;
}
