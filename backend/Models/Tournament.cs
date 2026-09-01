namespace Callahan.Api.Models;

public class Tournament
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }

    // Optional: which season this tournament belongs to. Set manually or by the
    // date-range attach sweep on SeasonsController.
    public int? SeasonId { get; set; }
    public Season? Season { get; set; }

    public List<Activity> Activities { get; set; } = new();
}
