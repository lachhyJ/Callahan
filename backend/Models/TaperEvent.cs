namespace Callahan.Api.Models;

public class TaperEvent
{
    public int Id { get; set; }
    public DateOnly Date { get; set; }
    public string? Name { get; set; }
    public int TaperDays { get; set; } = 10;
}
