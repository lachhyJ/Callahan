namespace Callahan.Api.Models;

public class Activity
{
    public int Id { get; set; }
    public DateOnly Date { get; set; }
    public ActivityType Type { get; set; }
    public ActivitySource Source { get; set; }
    public int DurationSeconds { get; set; }
    public decimal? DistanceKm { get; set; }
    public int? Calories { get; set; }
    public int? AvgHeartRate { get; set; }
    public string? Notes { get; set; }
    public string? GarminActivityId { get; set; }

    public int? ActivitySessionTypeId { get; set; }
    public ActivitySessionType? ActivitySessionType { get; set; }

    // Cached sum of ActivityLap.DistanceM where IntensityType == "ACTIVE",
    // recomputed whenever laps are (re)synced - so reading it never needs to
    // touch the Laps table.
    public decimal? HighSpeedDistanceM { get; set; }

    // Rough cone spacing Lachlan paces out himself before a High Speed
    // Intervals session - GPS/lap data can't give this directly (shuttle
    // turns make GPS distance an underestimate), so it's entered manually.
    public int? ConeDistanceM { get; set; }

    public List<ActivityLap> Laps { get; set; } = new();

    public DateTime? DeletedAt { get; set; }
}
