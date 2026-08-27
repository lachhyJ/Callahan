namespace Callahan.Api.Models;

public class Tournament
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }

    public List<Activity> Activities { get; set; } = new();
}
