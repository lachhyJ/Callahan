namespace Callahan.Api.Models;

public class ExerciseMuscleTarget
{
    public int Id { get; set; }

    public int ExerciseId { get; set; }
    public Exercise Exercise { get; set; } = null!;

    public MuscleGroup MuscleGroup { get; set; }

    // Secondary targets count for half weight in set-count-per-muscle-group
    // analytics (e.g. squats: Quads primary, Glutes/Hamstrings secondary).
    public bool IsPrimary { get; set; }
}
