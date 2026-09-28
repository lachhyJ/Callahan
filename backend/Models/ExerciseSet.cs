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
    // Stored as REAL, like every decimal in the model (AppDbContext), so
    // server-side comparisons and ordering are numeric.
    public decimal WeightKg { get; set; }
    public int SetOrder { get; set; }
    public SetType SetType { get; set; } = SetType.Normal;

    // The logged hold for a time set, in seconds. Non-null marks this row as a
    // time set: Reps is stored as 0 on those and must not be read as a count.
    // Reps stays a non-nullable int (the Reps = 0 convention avoids an
    // int -> int? migration across every read site); volume and e1RM skip any
    // row with a non-null DurationSeconds.
    public int? DurationSeconds { get; set; }
}
