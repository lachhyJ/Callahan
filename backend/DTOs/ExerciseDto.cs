namespace Callahan.Api.DTOs;

public record ExerciseDto(int Id, string Name, string Category, string? PrimaryMuscle);

public record CreateExerciseRequestDto(string Name, string Category);

public record ExerciseHistoryEntryDto(int WorkoutSessionId, DateOnly Date, string? Notes, List<PreviousSetDto> Sets);

public record ExerciseHistoryPageDto(List<ExerciseHistoryEntryDto> Entries, int TotalSessions);

public record ChartPointDto(DateOnly Date, decimal MaxWeightKg);

public record ExerciseStatsDto(
    string ExerciseName,
    string? PrimaryMuscle,
    decimal HeaviestWeightKg,
    decimal BestEstimated1Rm,
    decimal BestSetVolume,
    decimal BestSessionVolume,
    List<ChartPointDto> Chart);

public record ExercisePrDto(
    int ExerciseId,
    string ExerciseName,
    string Category,
    string? PrimaryMuscle,
    decimal HeaviestWeightKg,
    DateOnly HeaviestWeightDate,
    decimal BestEstimated1Rm);
