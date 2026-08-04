namespace Callahan.Api.DTOs;

public record WorkoutTemplateSummaryDto(int Id, string Name);

public record PreviousSetDto(int SetOrder, int Reps, decimal WeightKg);

public record WorkoutTemplateExerciseStartDto(
    int ExerciseId,
    string ExerciseName,
    int TargetSets,
    string TargetReps,
    List<PreviousSetDto> PreviousSets);

public record WorkoutTemplateStartDto(
    int TemplateId,
    string TemplateName,
    List<WorkoutTemplateExerciseStartDto> Exercises);
