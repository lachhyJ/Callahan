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
    int ActiveLapCount,
    decimal? HighSpeedDistanceKm,
    int? ConeDistanceM,
    // Lap-derived on/off-field split - all null unless this is an Ultimate
    // "Game" activity with synced laps. RawJson is deliberately not exposed
    // here. Defaulted so Running-only call sites don't have to pass them.
    int? OnFieldSeconds = null,
    int? OffFieldSeconds = null,
    int? MixedSeconds = null,
    int? PointsPlayed = null,
    decimal? OnFieldDistanceKm = null,
    int? AlternationViolations = null,
    string? LapClassifierMethod = null,
    decimal? OnFieldSpeedThresholdMps = null,
    int? LapClassifierVersion = null);

public record CreateActivityRequest(
    DateOnly Date,
    string Type,
    int DurationSeconds,
    decimal? DistanceKm,
    int? Calories,
    int? AvgHeartRate,
    string? Notes,
    string Source = "Manual",
    string? GarminActivityId = null,
    // Full Garmin activity summary, stored verbatim on the activity as a hedge
    // against fields not modelled yet. Only the Garmin sync sends this.
    string? RawJson = null);

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
    int? MaxHeartRate,
    string? FieldState);

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

public record ReclassifyChange(
    int ActivityId,
    DateOnly Date,
    string? MethodBefore,
    string? MethodAfter,
    int? PointsPlayed,
    int? AlternationViolations);

public record ReclassifyResponse(
    int ClassifierVersion,
    int Reclassified,
    List<ReclassifyChange> Changes);

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
