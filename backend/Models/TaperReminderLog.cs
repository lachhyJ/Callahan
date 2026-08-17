namespace Callahan.Api.Models;

// Durability record so TaperReminderService doesn't double-send a reminder for
// the same taper event on the same day across poll cycles or app restarts.
public class TaperReminderLog
{
    public int Id { get; set; }
    public int TaperEventId { get; set; }
    public TaperEvent TaperEvent { get; set; } = null!;
    public DateOnly Date { get; set; }
}
