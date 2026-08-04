namespace Callahan.Api.Models;

public class WorkoutTemplateExercise
{
    public int Id { get; set; }
    public int WorkoutTemplateId { get; set; }
    public WorkoutTemplate WorkoutTemplate { get; set; } = null!;

    public int ExerciseId { get; set; }
    public Exercise Exercise { get; set; } = null!;

    public int ExerciseOrder { get; set; }
    public int TargetSets { get; set; }
    public required string TargetReps { get; set; }
}
