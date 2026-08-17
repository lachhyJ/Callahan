namespace Callahan.Api.Models;

public class TaperCheckIn
{
    public int Id { get; set; }
    public int TaperEventId { get; set; }
    public TaperEvent TaperEvent { get; set; } = null!;

    public DateOnly Date { get; set; }
    public int Energy { get; set; }
    public int Soreness { get; set; }
    public int Motivation { get; set; }
    public string? Context { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
