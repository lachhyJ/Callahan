namespace Callahan.Api.Models;

// A competitive season: a manually-defined span (typically ~6 months) that
// groups several tournaments and builds to a target ("Nationals") tournament.
// Start, end, and the target are all set by hand — none is derived from the
// others.
public class Season
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }

    // The tournament that closes the season. Nullable and independent of
    // EndDate — the chart marks this tournament's date distinctly when set.
    public int? TargetTournamentId { get; set; }
    public Tournament? TargetTournament { get; set; }

    // Tournaments assigned to this season (via Tournament.SeasonId). Optional
    // association — a tournament need not belong to any season.
    public List<Tournament> Tournaments { get; set; } = new();
}
