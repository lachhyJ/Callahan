namespace Callahan.Api.DTOs;

public record ExerciseDto(int Id, string Name, string Category, string? PrimaryMuscle, bool IsAssisted);

// For the mid-session "add an exercise" picker: the full catalog, plus which
// templates (if any) each exercise is programmed into, so the picker can
// surface "from your other templates" first — the driving use case is an
// exercise prescribed in one day but performed in another.
public record PickableExerciseDto(int Id, string Name, string Category, bool IsAssisted, List<string> TemplateNames);

public record CreateExerciseRequestDto(string Name, string Category);

public record UpdateExerciseAssistedRequestDto(bool IsAssisted);

public record UpdateExerciseNameRequestDto(string Name);

public record ExerciseHistoryEntryDto(int WorkoutSessionId, DateOnly Date, string? Notes, List<PreviousSetDto> Sets);

public record ExerciseHistoryPageDto(List<ExerciseHistoryEntryDto> Entries, int TotalSessions);

public record ChartPointDto(DateOnly Date, decimal MaxWeightKg);

public record ExerciseStatsDto(
    string ExerciseName,
    string? PrimaryMuscle,
    bool IsAssisted,
    decimal HeaviestWeightKg,
    decimal BestEstimated1Rm,
    decimal BestSetVolume,
    decimal BestSessionVolume,
    List<ChartPointDto> Chart);
