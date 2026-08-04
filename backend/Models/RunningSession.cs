namespace Callahan.Api.Models;

public class RunningSession
{
    public int Id { get; set; }
    public DateOnly Date { get; set; }
    public decimal DistanceKm { get; set; }
    public int DurationSeconds { get; set; }
    public string? Notes { get; set; }
}
