namespace Callahan.Api.DTOs;

public record ActivityDto(
    int Id,
    DateOnly Date,
    string Type,
    string Source,
    int DurationSeconds,
    decimal? DistanceKm,
    int? Calories,
    int? AvgHeartRate,
    string? Notes,
    int? RunSessionTypeId,
    string? RunSessionTypeName);

public record CreateActivityRequest(
    DateOnly Date,
    string Type,
    int DurationSeconds,
    decimal? DistanceKm,
    int? Calories,
    int? AvgHeartRate,
    string? Notes,
    string Source = "Manual",
    string? GarminActivityId = null);

public record UpdateActivityRunSessionTypeRequest(int? RunSessionTypeId);

public record RunSessionTypeDto(int Id, string Name);
