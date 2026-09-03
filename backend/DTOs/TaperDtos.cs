namespace Callahan.Api.DTOs;

// The taper-facing view of a Tournament. `Date` is the tournament's StartDate -
// what a taper counts down to - so the taper surfaces don't have to care that
// the underlying row spans a weekend.
public record TaperEventDto(int Id, DateOnly Date, string? Name, int TaperDays, int DaysUntil, decimal? PlannedReductionPercent);

// EndDate is optional: the taper page's form asks for a single date, and a
// tournament created there ends the day it starts unless told otherwise.
public record CreateTaperEventRequest(DateOnly Date, DateOnly? EndDate, string? Name, int TaperDays);

public record TaperRecommendationDto(
    TaperEventDto? UpcomingEvent,
    string Phase,
    string Message,
    decimal? GymTargetPct,
    decimal? GymBaselineVolume,
    decimal? GymThisWeekVolume,
    decimal? RunTargetPct,
    decimal? RunBaselineDistanceKm,
    decimal? RunThisWeekDistanceKm,
    int TapersCompleted);

public record TaperCheckInDto(int Id, DateOnly Date, int Energy, int Soreness, int Motivation, string? Context, bool IsDebrief);

public record UpsertTaperCheckInRequest(DateOnly Date, int Energy, int Soreness, int Motivation, string? Context);

public record TaperConsultRequest(string Question);

public record TaperConsultResponseDto(string Answer, bool ComparedToPriorTaper);
