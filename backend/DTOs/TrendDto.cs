namespace Callahan.Api.DTOs;

public record TrendPointDto(DateOnly MonthStart, decimal VolumeKg, int GymSessions, int RunSessions);

public record LiftTrendDto(
    int ExerciseId,
    string ExerciseName,
    decimal EarliestWeightKg,
    DateOnly EarliestMonth,
    decimal LatestWeightKg,
    DateOnly LatestMonth,
    decimal DeltaKg);

public record RunTypeTrendDto(
    int RunSessionTypeId,
    string RunSessionTypeName,
    int SessionCount,
    decimal TotalDistanceKm,
    decimal AvgDistanceKm);
