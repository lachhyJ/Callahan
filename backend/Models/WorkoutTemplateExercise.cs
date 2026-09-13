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

    // Warmup sets prepended before the TargetSets working rows at session
    // init, always in addition to the prescribed working-set count, never
    // replacing part of it. An int rather than a bool so a second warmup
    // later doesn't need another migration.
    public int WarmupSets { get; set; }
    public required string TargetReps { get; set; }
    public int RestSeconds { get; set; }
    public string? Tempo { get; set; }

    // The prescribed hold for a time-based slot, in seconds (e.g. 45). Null on
    // rep-based slots. TargetReps stays a required string but is "" / "—" for
    // time-based slots.
    public int? TargetDurationSeconds { get; set; }

    // Structured rep-range bounds parsed from TargetReps, used for progression
    // readiness. Null on both means "never flag this slot" (AMRAP, time-based
    // slots). TargetReps itself stays the free-text display string.
    public int? TargetRepsMin { get; set; }
    public int? TargetRepsMax { get; set; }

    // A standing cue for this program slot (e.g. "Workout 2's Incline DB
    // Press"), not tied to any single session — distinct from ExerciseNote,
    // which is per-session. Edited in place from the active workout or the
    // exercise detail page.
    public string? Cue { get; set; }

    // True when this slot runs straight into the next one (by ExerciseOrder) as
    // a superset — no rest between them. A superset is a maximal run of slots
    // where every member but the last carries this flag; the last member's rest
    // is the one that fires per round. Consecutive-only by construction:
    // adjacency is ExerciseOrder, so there is nothing to store beyond this bit.
    // Set from the active workout's Rearrange mode.
    public bool SupersetWithNext { get; set; }
}
