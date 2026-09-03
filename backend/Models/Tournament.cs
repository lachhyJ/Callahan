namespace Callahan.Api.Models;

// A tournament weekend. One row serves both directions: forward-looking (the
// thing you taper toward, via TaperDays) and retrospective (the grouping label
// for the Ultimate activities that happened inside its date range). These were
// two disconnected entities until 2026-09-04 - Tournament here and a separate
// TaperEvent - which meant every tournament was entered twice with no link
// between the two records.
public class Tournament
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }

    // Length of the taper leading into StartDate, in days. Null means this
    // tournament is not a taper target - the normal state for a backfilled
    // past tournament, and why this is nullable rather than defaulted: adding
    // a tournament on the games list must not silently create a taper.
    public int? TaperDays { get; set; }

    // Planned volume reduction, fixed at the time TaperDays was set, mirroring
    // TaperPhaseCalculator's target-pct semantics for a taper of this length
    // (1 - peak_taper's TargetPct, i.e. the deepest planned cut before game
    // day) so a finished taper's "planned vs actual" comparison has a stable
    // number to compare against instead of one that keeps recomputing as
    // "days until" drifts. Null whenever TaperDays is null.
    public decimal? PlannedReductionPercent { get; set; }

    // Optional: which season this tournament belongs to. Set manually or by the
    // date-range attach sweep on SeasonsController.
    public int? SeasonId { get; set; }
    public Season? Season { get; set; }

    public List<Activity> Activities { get; set; } = new();

    // Taper check-ins and reminder logs are owned by the tournament (cascade on
    // delete), unlike Activities which merely reference it.
    public List<TaperCheckIn> TaperCheckIns { get; set; } = new();
}
