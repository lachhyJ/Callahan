namespace Callahan.Api.Models;

// A dated record that a routine was done. One row per routine per day at most,
// enforced by a unique index - marking the same day twice is idempotent rather
// than an error, since the obvious user action is tapping the same button again.
public class RoutineCompletion
{
    public int Id { get; set; }
    public int RoutineId { get; set; }
    public Routine Routine { get; set; } = null!;

    public DateOnly Date { get; set; }

    // Optional, and the reason this is a record rather than a checkbox: "left
    // ankle felt loose today" is the thing worth having in three months.
    public string? Notes { get; set; }
}
