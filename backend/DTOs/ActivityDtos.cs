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
    int? ActivitySessionTypeId,
    string? ActivitySessionTypeName);

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

public record UpdateActivitySessionTypeRequest(int? ActivitySessionTypeId);

public record ActivitySessionTypeDto(int Id, string Name, string ActivityType);

public record DeletedActivityDto(
    int Id,
    DateOnly Date,
    string Type,
    string Source,
    int DurationSeconds,
    decimal? DistanceKm,
    string? Notes,
    int? ActivitySessionTypeId,
    string? ActivitySessionTypeName,
    DateTime DeletedAt);
