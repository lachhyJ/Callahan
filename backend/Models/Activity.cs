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

    public DateTime? DeletedAt { get; set; }
}
