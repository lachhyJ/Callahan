namespace Callahan.Api.DTOs;

public record ExerciseDto(int Id, string Name, string Category);

public record CreateExerciseRequestDto(string Name, string Category);

public record ExerciseHistoryEntryDto(int WorkoutSessionId, DateOnly Date, List<PreviousSetDto> Sets);
