namespace Callahan.Api.DTOs;

public record ExerciseSetDto(int Id, int ExerciseId, string ExerciseName, int Reps, decimal WeightKg, int SetOrder, string SetType);

public record ExerciseNoteDto(int ExerciseId, string ExerciseName, string Notes);

public record WorkoutSessionSummaryDto(int Id, DateOnly Date, string? Name, string? Notes, int SetCount, string? TemplateName, string? TemplateSubtitle, DateTime? StartedAt, DateTime? FinishedAt, string? CategorySummary);

public record WorkoutSessionDetailDto(int Id, DateOnly Date, string? Name, string? Notes, DateTime? StartedAt, DateTime? FinishedAt, string? TemplateName, string? TemplateSubtitle, string? CategorySummary, List<ExerciseSetDto> Sets, List<ExerciseNoteDto> ExerciseNotes);

public record UpdateWorkoutSessionNameRequest(string? Name);

public record DeletedWorkoutSessionDto(int Id, DateOnly Date, string? Name, int SetCount, string? TemplateName, string? TemplateSubtitle, string? CategorySummary, DateTime DeletedAt);

public record CreateExerciseSetRequest(int ExerciseId, int Reps, decimal WeightKg, int SetOrder, string SetType);

public record CreateExerciseNoteRequest(int ExerciseId, string Notes);

public record CreateWorkoutSessionRequest(
    DateOnly Date,
    string? Name,
    string? Notes,
    int? WorkoutTemplateId,
    DateTime? StartedAt,
    DateTime? FinishedAt,
    List<CreateExerciseSetRequest> Sets,
    List<CreateExerciseNoteRequest>? ExerciseNotes);

public record WeeklyVolumeDto(DateOnly WeekStart, decimal Volume);
