namespace Callahan.Api.DTOs;

public record WorkoutTemplateSummaryDto(int Id, string Name, string Subtitle);

public record PreviousSetDto(int SetOrder, int Reps, decimal WeightKg, string SetType, int? DurationSeconds = null);

public record WorkoutTemplateExerciseStartDto(
    int WorkoutTemplateExerciseId,
    int ExerciseId,
    string ExerciseName,
    int TargetSets,
    int WarmupSets,
    string TargetReps,
    int RestSeconds,
    string? Tempo,
    string? Cue,
    string? PrimaryMuscle,
    bool IsAssisted,
    bool IsTimeBased,
    bool IsPerSide,
    int PerSideDelaySeconds,
    int? TargetDurationSeconds,
    bool SupersetWithNext,
    List<PreviousSetDto> PreviousSets,
    bool ReadyToProgress);

public record WorkoutTemplateStartDto(
    int TemplateId,
    string TemplateName,
    string TemplateSubtitle,
    List<WorkoutTemplateExerciseStartDto> Exercises);

public record UpdateCueRequest(string? Cue);

public record UpdateRestSecondsRequest(int RestSeconds);

public record ExerciseCueDto(int WorkoutTemplateExerciseId, string TemplateName, string? Cue);

// One template slot's position and superset link, as sent back from the active
// workout's Rearrange mode. ExerciseOrder is the 0-based index among the
// template's own slots (ad-hoc session additions are not included).
public record TemplateLayoutItemDto(int WorkoutTemplateExerciseId, int ExerciseOrder, bool SupersetWithNext);

public record UpdateTemplateLayoutRequest(List<TemplateLayoutItemDto> Items);
