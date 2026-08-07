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
    public int RestSeconds { get; set; }
    public string? Tempo { get; set; }

    // A standing cue for this program slot (e.g. "Workout 2's Incline DB
    // Press"), not tied to any single session — distinct from ExerciseNote,
    // which is per-session. Edited in place from the active workout or the
    // exercise detail page.
    public string? Cue { get; set; }
}
