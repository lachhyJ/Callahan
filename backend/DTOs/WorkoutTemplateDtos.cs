namespace Callahan.Api.DTOs;

public record WorkoutTemplateSummaryDto(int Id, string Name, string Subtitle);

public record PreviousSetDto(int SetOrder, int Reps, decimal WeightKg, string SetType);

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
    List<PreviousSetDto> PreviousSets);

public record WorkoutTemplateStartDto(
    int TemplateId,
    string TemplateName,
    string TemplateSubtitle,
    List<WorkoutTemplateExerciseStartDto> Exercises);

public record UpdateCueRequest(string? Cue);

public record UpdateRestSecondsRequest(int RestSeconds);

public record ExerciseCueDto(int WorkoutTemplateExerciseId, string TemplateName, string? Cue);
