namespace Callahan.Api.Models;

public class ActivityLap
{
    public int Id { get; set; }
    public int ActivityId { get; set; }
    public Activity Activity { get; set; } = null!;

    public int LapIndex { get; set; }

    // Garmin's own work/rest labelling for the lap - WARMUP, ACTIVE,
    // RECOVERY, REST, COOLDOWN - confirmed via --dump-laps against a real
    // High Speed Intervals session (structured workout on the watch, so
    // Garmin auto-laps each rep). High-speed distance is the sum of
    // DistanceM across laps where this is "ACTIVE" - no speed-threshold
    // heuristic needed, Garmin already did that classification.
    public string? IntensityType { get; set; }

    public decimal? DistanceM { get; set; }
    public decimal? DurationSeconds { get; set; }
    public decimal? MovingDurationSeconds { get; set; }
    public decimal? AvgSpeedMps { get; set; }
    public decimal? MaxSpeedMps { get; set; }
    public int? AvgHeartRate { get; set; }
    public int? MaxHeartRate { get; set; }

    // Absolute lap start (Garmin lapDTOs.startTimeGMT). Needed to line a lap up
    // against the GPS track for geometric on/off-field labelling - deriving it
    // by summing prior DurationSeconds drifts (elapsed vs moving). Null on
    // every row synced before this column existed.
    public DateTime? StartTimeGmt { get; set; }

    // On/off-field call for a lap of an Ultimate "Game" activity, from
    // LapFieldClassifier (see LapFieldState for the values). Null on runs and
    // on Ultimate sessions that aren't Games. String rather than a bool so
    // "Unknown" and "Mixed" (a lap welding a point to a sideline stint after a
    // missed lap press) stay distinct, and so a new state needs no migration.
    public string? FieldState { get; set; }
}
