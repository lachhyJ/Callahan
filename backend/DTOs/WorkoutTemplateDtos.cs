namespace Callahan.Api.DTOs;

public record WorkoutTemplateSummaryDto(int Id, string Name);

public record PreviousSetDto(int SetOrder, int Reps, decimal WeightKg, string SetType);

public record WorkoutTemplateExerciseStartDto(
    int ExerciseId,
    string ExerciseName,
    int TargetSets,
    string TargetReps,
    int RestSeconds,
    string? Tempo,
    List<PreviousSetDto> PreviousSets);

public record WorkoutTemplateStartDto(
    int TemplateId,
    string TemplateName,
    List<WorkoutTemplateExerciseStartDto> Exercises);
