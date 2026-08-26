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
    int? LapClassifierVersion = null,
    // Number of GPS samples in the activity's synced track (0 = no track). The
    // sync uses this to fetch the stream only once per activity.
    int TrackSampleCount = 0);

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
    int? MaxHeartRate,
    // Garmin lapDTOs.startTimeGMT - absolute lap start, the join key against
    // the GPS track. Null from the pre-track sync.
    DateTime? StartTimeGmt = null);

public record UpsertActivityLapsRequest(List<UpsertActivityLapRequest> Laps);

public record ActivityLapsResponse(List<ActivityLapDto> Laps, decimal? HighSpeedDistanceKm);

// PUT /api/activities/{id}/track body. Samples are four parallel arrays; T is
// seconds from StartEpochMs. Same shape the sync builds and the test fixtures
// carry.
public record TrackSamplesDto(
    List<double> T,
    List<double> Lat,
    List<double> Lon,
    List<double> Spd);

public record UpsertTrackRequest(
    long StartEpochMs,
    int SampleCount,
    decimal? MedianSpacingSec,
    TrackSamplesDto Samples);

public record ActivityTrackResponse(
    int SampleCount,
    int? OnFieldSeconds,
    int? OffFieldSeconds,
    int? PointsPlayed,
    string? LapClassifierMethod);

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

// GET /api/activities/{id}/field-timeline - the on/off segments FieldGeometry
// computes but ApplyLapDerivedAggregates discards after aggregating. Recomputed
// on every read from the stored raw track rather than persisted, so it always
// reflects the current FieldGeometryOptions tuning. StartSec/EndSec are
// relative to the track's start, not absolute epochs.
public record FieldSegmentDto(bool OnField, int StartSec, int EndSec);

public record FieldTimelineDto(
    int TotalSeconds,
    List<FieldSegmentDto> Segments,
    int GeometryVersion);

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
