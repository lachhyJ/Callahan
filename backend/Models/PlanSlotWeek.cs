namespace Callahan.Api.Models;

// A slot's per-week deviation from the program's default: moved to another day,
// ticked by hand, or dropped. Written only when something is actually changed -
// a week nobody has touched has no rows at all and renders straight from the
// defaults.
//
// Keyed on the Monday of the week rather than on a date range, so "which week is
// this" is a single comparison and two clients can't disagree about it.
public class PlanSlotWeek
{
    public int Id { get; set; }

    public int PlanSlotId { get; set; }
    public PlanSlot PlanSlot { get; set; } = null!;

    public DateOnly WeekStart { get; set; }

    // Null means "wherever the program puts it". Set when the slot has been
    // moved for this week only; next week returns to the default rather than
    // inheriting the change, since the reason for a move (a shift, a one-off)
    // rarely repeats.
    public int? DayOfWeek { get; set; }

    public PlanSlotStatus Status { get; set; }
}
