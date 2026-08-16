namespace Callahan.Api.DTOs;

public record TaperEventDto(int Id, DateOnly Date, string? Name, int TaperDays, int DaysUntil);

public record CreateTaperEventRequest(DateOnly Date, string? Name, int TaperDays);

public record TaperRecommendationDto(
    TaperEventDto? UpcomingEvent,
    string Phase,
    string Message,
    decimal? GymTargetPct,
    decimal? GymBaselineVolume,
    decimal? GymThisWeekVolume,
    decimal? RunTargetPct,
    decimal? RunBaselineDistanceKm,
    decimal? RunThisWeekDistanceKm);
