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

// Distance fields are nullable for the same reason as RunTypeSummaryDto's:
// a total (or average) GPS distance is only meaningful for continuous
// running. RunningMetrics.ShapeFor decides, so this and the monthly report
// can't say contradictory things about the same sessions.
public record RunTypeTrendDto(
    int RunSessionTypeId,
    string RunSessionTypeName,
    int SessionCount,
    decimal? TotalDistanceKm,
    decimal? AvgDistanceKm,
    decimal? HighSpeedDistanceKm,
    int? WorkRepCount);

public record SeasonStrengthDto(
    List<SeasonMonthDto> Months,
    List<ExerciseTrajectoryDto> Series,
    List<SeasonBandDto> Seasons,
    List<TournamentBandDto> Bands);

public record SeasonMonthDto(DateOnly MonthStart, decimal RunKm, int UltimateLivePlayMin);

public record ExerciseTrajectoryDto(
    int ExerciseId,
    string ExerciseName,
    decimal BaselineE1Rm,
    bool IsPrimary,
    List<TrajectoryPointDto> Points);

public record TrajectoryPointDto(DateOnly MonthStart, decimal E1Rm, decimal PctFromBaseline);

public record SeasonBandDto(string Name, DateOnly Start, DateOnly End, DateOnly? TargetDate);

public record TournamentBandDto(string Name, DateOnly Start, DateOnly End);
