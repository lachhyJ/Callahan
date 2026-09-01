namespace Callahan.Api.DTOs;

public record MonthlyReportListEntryDto(int Year, int Month, bool IsLocked, bool Viewed, string HeadlineVerdict);

// Family groups the label into the three things that are actually
// substitutable for each other: gym templates, run types, Ultimate session
// types. Comparing counts ACROSS families is meaningless (a club training is
// not an alternative to an interval session), which the next-month
// "rebalance?" question used to do.
public record SessionTypeCountDto(string Label, int Count, string Family);

public static class SessionFamily
{
    public const string Gym = "Gym";
    public const string Running = "Running";
    public const string Ultimate = "Ultimate";
}
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

// A real set that was actually performed, plus its estimated 1RM where the
// estimate is trustworthy for that exercise (null otherwise - see LiftBasis).
// Reporting the set alongside the estimate is deliberate: an e1RM on its own
// is a number that was never lifted.
public record LiftSetDto(decimal WeightKg, int Reps, decimal? E1Rm);

// Basis is LiftBasis as a string, so the UI can label what it's comparing -
// a +13% on set volume is not the same claim as a +13% on e1RM.
public record PrDto(int ExerciseId, string ExerciseName, LiftSetDto Best, LiftSetDto? Previous, string Basis, DateOnly Date);
public record MoverDto(int ExerciseId, string ExerciseName, LiftSetDto From, LiftSetDto To, decimal? DeltaPercent, string Basis, DateOnly LastSessionDate);
public record StallDto(int ExerciseId, string ExerciseName, int SessionsFlat, DateOnly LastSessionDate, LiftSetDto Best, string Basis);

// WindowSessions is how many of each exercise's most recent sessions the
// movers/stalls windows look at - surfaced so the UI can say so rather than
// letting a reader assume "movers" means "moved during this month". The
// window is deliberately NOT clipped to the report month (see
// MonthlyReportBuilder's class comment).
public record LoadProgressionSectionDto(
    List<PrDto> Prs,
    List<MoverDto> Movers,
    List<StallDto> Stalls,
    List<string> ZeroSetProgramExercises,
    int WindowSessions
);

// Which fields are populated depends on the session type - see
// RunningMetrics. Totalling GPS distance and elapsed duration is only
// meaningful for continuous running; for rep-based sessions those totals
// mislead (GPS under-measures shuttle turns, elapsed time counts standing
// rest), so they come back null and the rep-based fields carry the work
// instead. Nulls mean "not meaningful for this type", not "missing".
public record RunTypeSummaryDto(
    string TypeName,
    int Count,
    decimal? TotalDistanceKm,
    int? TotalDurationSeconds,
    decimal? HighSpeedDistanceKm,
    int? WorkRepCount);
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
    int NightsUnder7h
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
