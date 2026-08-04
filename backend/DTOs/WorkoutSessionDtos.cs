namespace Callahan.Api.DTOs;

public record ExerciseSetDto(int Id, int ExerciseId, string ExerciseName, int Reps, decimal WeightKg, int SetOrder, string SetType);

public record ExerciseNoteDto(int ExerciseId, string ExerciseName, string Notes);

public record WorkoutSessionSummaryDto(int Id, DateOnly Date, string? Notes, int SetCount, string? TemplateName);

public record WorkoutSessionDetailDto(int Id, DateOnly Date, string? Notes, List<ExerciseSetDto> Sets, List<ExerciseNoteDto> ExerciseNotes);

public record CreateExerciseSetRequest(int ExerciseId, int Reps, decimal WeightKg, int SetOrder, string SetType);

public record CreateExerciseNoteRequest(int ExerciseId, string Notes);

public record CreateWorkoutSessionRequest(
    DateOnly Date,
    string? Notes,
    int? WorkoutTemplateId,
    List<CreateExerciseSetRequest> Sets,
    List<CreateExerciseNoteRequest>? ExerciseNotes);
