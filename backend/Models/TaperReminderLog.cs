namespace Callahan.Api.Models;

// Durability record so TaperReminderService doesn't double-send a reminder for
// the same tournament on the same day across poll cycles or app restarts.
public class TaperReminderLog
{
    public int Id { get; set; }
    public int TournamentId { get; set; }
    public Tournament Tournament { get; set; } = null!;
    public DateOnly Date { get; set; }
}
