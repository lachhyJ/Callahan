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
    string? Notes);

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
