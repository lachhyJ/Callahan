namespace Callahan.Api.DTOs;

public record MonthlyReportListEntryDto(int Year, int Month, bool IsLocked, bool Viewed, string HeadlineVerdict);

public record SessionTypeCountDto(string Label, int Count);
public record WeeklyTargetHitDto(string Type, string Label, int WeeksHit, int WeeksTotal);

public record ConsistencySectionDto(
    int TotalSessions,
    decimal WeeksInMonth,
    decimal SessionsPerWeek,
    decimal TrailingSessionsPerWeek,
    List<SessionTypeCountDto> SessionsByType,
    List<WeeklyTargetHitDto> WeeklyTargets,
    int DaysTrained,
    int DaysInMonth
);

public record PrDto(int ExerciseId, string ExerciseName, decimal E1Rm, DateOnly Date, decimal? PreviousE1Rm);
public record MoverDto(int ExerciseId, string ExerciseName, decimal FromE1Rm, decimal ToE1Rm, decimal DeltaPercent);
public record StallDto(int ExerciseId, string ExerciseName, int SessionsFlat, DateOnly LastSessionDate);

public record LoadProgressionSectionDto(
    List<PrDto> Prs,
    List<MoverDto> Movers,
    List<StallDto> Stalls,
    List<string> ZeroSetProgramExercises
);

public record RunTypeSummaryDto(string TypeName, int Count, decimal TotalDistanceKm, int TotalDurationSeconds);
public record RunningSectionDto(List<RunTypeSummaryDto> ByType);

public record BalanceSectionDto(string? FlaggedLine);

public record ContextSectionDto(List<string> Tournaments, int? LongestGapDays, DateOnly? LongestGapStart, DateOnly? LongestGapEnd);

public record TaperSectionDto(
    string EventName,
    DateOnly EventDate,
    string Overlap, // "partial" | "substantial"
    decimal? PlannedReductionPercent,
    decimal? ActualReductionPercent,
    int CheckInsCompleted,
    int CheckInsExpected,
    decimal RawSessionsPerWeek,
    decimal ExclTaperWeeksSessionsPerWeek
);

// One recovery metric: this month's average against the trailing 3-month
// average, with a recovery-oriented direction ("below" = worse than baseline).
// Avgs are null when the month / trailing window had too few readings.
public record WellnessMetricDto(string Key, string Label, decimal? MonthAvg, decimal? TrailingAvg, string Direction);

public record WellnessSectionDto(
    List<WellnessMetricDto> Metrics,
    int NightsLogged,
    int DaysInMonth,
    int NightsUnder7h,
    string? LoadVsRecoveryLine
);

public record MonthlyReportDto(
    int Year,
    int Month,
    bool IsLocked,
    bool IsProvisional,
    DateTime ComputedAt,
    DateTime? ViewedAt,
    string HeadlineVerdict,
    ConsistencySectionDto Consistency,
    LoadProgressionSectionDto LoadProgression,
    RunningSectionDto Running,
    BalanceSectionDto Balance,
    ContextSectionDto Context,
    List<TaperSectionDto> TaperOverlaps,
    List<string> NextMonthQuestions,
    WellnessSectionDto? Wellness = null
);
