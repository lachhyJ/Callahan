namespace Callahan.Api.DTOs;

public record ExerciseSetDto(int Id, int ExerciseId, string ExerciseName, int Reps, decimal WeightKg, int SetOrder);

public record WorkoutSessionSummaryDto(int Id, DateOnly Date, string? Notes, int SetCount);

public record WorkoutSessionDetailDto(int Id, DateOnly Date, string? Notes, List<ExerciseSetDto> Sets);

public record CreateExerciseSetRequest(int ExerciseId, int Reps, decimal WeightKg, int SetOrder);

public record CreateWorkoutSessionRequest(DateOnly Date, string? Notes, List<CreateExerciseSetRequest> Sets);
