namespace Callahan.Api.Models;

public class TaperEvent
{
    public int Id { get; set; }
    public DateOnly Date { get; set; }
    public string? Name { get; set; }
    public int TaperDays { get; set; } = 10;

    // Planned volume reduction at creation time, mirroring
    // TaperPhaseCalculator's target-pct semantics for a taper of this length
    // (1 - peak_taper's TargetPct, i.e. the deepest planned cut before game
    // day) so a finished taper's "planned vs actual" comparison has a fixed
    // number to compare against instead of one that keeps recomputing as
    // "days until" drifts. Null for events created before this field existed
    // — no backfill, those just don't show a planned figure.
    public decimal? PlannedReductionPercent { get; set; }
}
