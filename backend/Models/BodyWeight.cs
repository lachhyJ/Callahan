namespace Callahan.Api.Models;

// One weigh-in per calendar day (the latest of that day wins), pulled from Garmin
// Connect by the nightly sync. A table of its own rather than a DailyWellness column:
// the wellness upsert overwrites every field on every PUT, so a weight stored there
// would be nulled by the next sync of that day. Nothing reads it yet; it is here so
// the history exists when something needs it.
public class BodyWeight
{
    public int Id { get; set; }
    public DateOnly Date { get; set; }
    public double WeightKg { get; set; }

    // Where Garmin says the reading came from ("MANUAL", "INDEX_SCALE", ...), kept raw.
    public string? Source { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
