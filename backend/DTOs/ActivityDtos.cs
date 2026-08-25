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
    string? ActivitySessionTypeName,
    int LapCount,
    decimal? HighSpeedDistanceKm,
    int? ConeDistanceM);

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

public record ActivityLapDto(
    int LapIndex,
    string? IntensityType,
    decimal? DistanceM,
    decimal? DurationSeconds,
    decimal? MovingDurationSeconds,
    decimal? AvgSpeedMps,
    decimal? MaxSpeedMps,
    int? AvgHeartRate,
    int? MaxHeartRate);

public record UpsertActivityLapRequest(
    int LapIndex,
    string? IntensityType,
    decimal? DistanceM,
    decimal? DurationSeconds,
    decimal? MovingDurationSeconds,
    decimal? AvgSpeedMps,
    decimal? MaxSpeedMps,
    int? AvgHeartRate,
    int? MaxHeartRate);

public record UpsertActivityLapsRequest(List<UpsertActivityLapRequest> Laps);

public record ActivityLapsResponse(List<ActivityLapDto> Laps, decimal? HighSpeedDistanceKm);

public record UpdateConeDistanceRequest(int? ConeDistanceM);

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
