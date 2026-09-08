namespace Callahan.Api.Models;

public class Exercise
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public ExerciseCategory Category { get; set; }
    public bool IsAssisted { get; set; }

    // Held for time rather than counted in reps (planks, hollow holds). Set at
    // the catalog level so custom/ad-hoc sessions with no template slot still
    // pick it up. Sibling of IsAssisted.
    public bool IsTimeBased { get; set; }

    // The timer prompts for a second side (Copenhagen plank, single-leg holds).
    // Only the active-workout timer reads this for now.
    public bool IsPerSide { get; set; }

    // Seconds the active-workout hold timer waits between side one ending and
    // side two auto-starting (a beep marks each). Only meaningful while
    // IsPerSide is on, but stored independently so toggling per-side off and
    // back on keeps the chosen gap. Default 8.
    public int PerSideDelaySeconds { get; set; } = 8;

    public ICollection<ExerciseSet> Sets { get; set; } = [];
    public ICollection<ExerciseMuscleTarget> MuscleTargets { get; set; } = [];
}
