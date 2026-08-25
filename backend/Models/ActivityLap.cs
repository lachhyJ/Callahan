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
}
