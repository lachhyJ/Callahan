namespace Callahan.Api.Models;

// One line of the lock-screen quotes widget's list. Soft-deleted like sessions and
// activities (7-day window, see QuotesController), but with no Recently Deleted
// screen: the Undo toast is the whole recovery surface.
public class Quote
{
    public int Id { get; set; }
    public required string Text { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? DeletedAt { get; set; }
}
