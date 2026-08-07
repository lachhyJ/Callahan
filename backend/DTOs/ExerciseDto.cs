namespace Callahan.Api.DTOs;

public record ExerciseDto(int Id, string Name, string Category);

public record CreateExerciseRequestDto(string Name, string Category);

public record ExerciseHistoryEntryDto(int WorkoutSessionId, DateOnly Date, List<PreviousSetDto> Sets);

public record ExerciseHistoryPageDto(List<ExerciseHistoryEntryDto> Entries, int TotalSessions);

public record ChartPointDto(DateOnly Date, decimal MaxWeightKg);

public record ExerciseStatsDto(
    string ExerciseName,
    decimal HeaviestWeightKg,
    decimal BestEstimated1Rm,
    decimal BestSetVolume,
    decimal BestSessionVolume,
    List<ChartPointDto> Chart);
