namespace Callahan.Api.DTOs;

public record TrendPointDto(DateOnly MonthStart, decimal VolumeKg, int GymSessions, int RunSessions);

// Earliest/Latest are the actual best sets of those months, on whichever
// basis the exercise supports (Basis is LiftBasis as a string). DeltaPercent
// is null for assisted/bodyweight lifts, where the underlying score is an
// ordering rather than a magnitude; DeltaKg still carries the load change,
// which for an assisted lift reads as assistance coming off.
public record LiftTrendDto(
    int ExerciseId,
    string ExerciseName,
    LiftSetDto Earliest,
    DateOnly EarliestMonth,
    LiftSetDto Latest,
    DateOnly LatestMonth,
    decimal? DeltaPercent,
    decimal DeltaKg,
    string Basis);

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

// One calendar month of Ultimate distance: whole-recording GPS km, a
// per-session-type breakdown of what made it up, a count of Ultimate sessions
// that carried no GPS distance (indoor / non-GPS / manual - not estimated),
// and run km alongside for a combined view. Descriptive only.
public record UltimateDistanceMonthDto(
    DateOnly MonthStart,
    decimal UltimateKm,
    decimal RunKm,
    int UltimateSessionsWithoutDistance,
    List<UltimateDistanceByTypeDto> ByType);

// Km is 0 for a type whose sessions this month all lacked GPS distance -
// SessionsWithoutDistance then carries the count. Sessions is every session of
// that type in the month, with or without distance.
public record UltimateDistanceByTypeDto(
    string TypeName,
    decimal Km,
    int Sessions,
    int SessionsWithoutDistance);
