namespace Callahan.Api.Models;

// Snapshot storage for a computed monthly report. Before day 8 of the
// following month, reports are computed live on every read (no row here) and
// marked provisional by the controller. From day 8 onward, the first read
// for that month computes it once, stores the full DTO as JSON in
// ReportJson, and every read after that returns the stored snapshot
// unchanged — a lock is a real snapshot, not a flag over a still-recomputing
// report. JSON blob rather than structured columns: this report has many
// nested, optional sections (taper only sometimes present, etc.) that would
// otherwise need a wide sparse table or several join tables for something
// that's never queried piecemeal — it's always read whole, by month.
public class MonthlyReport
{
    public int Id { get; set; }
    public int Year { get; set; }
    public int Month { get; set; } // 1-12

    public string ReportJson { get; set; } = "{}";
    public DateTime ComputedAt { get; set; }

    public DateTime? ViewedAt { get; set; }
}
