namespace Callahan.Api.Models;

public class ExerciseSet
{
    public int Id { get; set; }
    public int WorkoutSessionId { get; set; }
    public WorkoutSession WorkoutSession { get; set; } = null!;

    public int ExerciseId { get; set; }
    public Exercise Exercise { get; set; } = null!;

    public int Reps { get; set; }
    public decimal WeightKg { get; set; }
    public int SetOrder { get; set; }
}
