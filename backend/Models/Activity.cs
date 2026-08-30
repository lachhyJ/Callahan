namespace Callahan.Api.Models;

public class Activity
{
    public int Id { get; set; }
    public DateOnly Date { get; set; }
    public ActivityType Type { get; set; }
    public ActivitySource Source { get; set; }
    public int DurationSeconds { get; set; }
    public decimal? DistanceKm { get; set; }
    public int? Calories { get; set; }
    public int? AvgHeartRate { get; set; }
    public string? Notes { get; set; }
    public string? GarminActivityId { get; set; }

    public int? ActivitySessionTypeId { get; set; }
    public ActivitySessionType? ActivitySessionType { get; set; }

    // Which tournament this game belongs to, if any. Ultimate-only in
    // practice (set by the date-range attach sweep or the manual picker) but
    // not type-constrained at the DB level - a stray non-Ultimate row here is
    // harmless, not worth a check constraint for.
    public int? TournamentId { get; set; }
    public Tournament? Tournament { get; set; }

    // Cached sum of ActivityLap.DistanceM where IntensityType == "ACTIVE",
    // recomputed whenever laps are (re)synced - so reading it never needs to
    // touch the Laps table.
    public decimal? HighSpeedDistanceM { get; set; }

    // Rough cone spacing Lachlan paces out himself before a High Speed
    // Intervals session - GPS/lap data can't give this directly (shuttle
    // turns make GPS distance an underestimate), so it's entered manually.
    public int? ConeDistanceM { get; set; }

    // Lap-derived on/off-field split for Ultimate "Game" activities, computed
    // by LapFieldClassifier at lap-sync time and on session-type change, and
    // recomputable in bulk via POST /api/activities/laps/reclassify with no
    // Garmin traffic. All null on runs and on non-Game Ultimate sessions.
    public int? OnFieldSeconds { get; set; }
    public int? OffFieldSeconds { get; set; }
    public int? MixedSeconds { get; set; }
    public int? PointsPlayed { get; set; }
    public decimal? OnFieldDistanceM { get; set; }
    // On-field seconds spent inside a detected point ("live play"), as opposed
    // to OnFieldSeconds which also counts waiting on the line between points.
    // Always <= OnFieldSeconds. See GeometryResult.LivePlaySeconds.
    public int? LivePlaySeconds { get; set; }
    // GPS distance (metres) covered inside detected points - the live-play
    // counterpart to OnFieldDistanceM. Always <= OnFieldDistanceM.
    public decimal? LivePlayDistanceM { get; set; }
    // Count of adjacent lap pairs that shared an on/off state - i.e. missed
    // lap presses. 0 means a clean capture. Doubles as feedback on lapping.
    public int? AlternationViolations { get; set; }
    // Audit trail: which classifier path ran (LapClassifierMethod.*), what
    // speed boundary it derived, and at what algorithm version - so a wrong
    // call is diagnosable later without re-running anything.
    public string? LapClassifierMethod { get; set; }
    public decimal? OnFieldSpeedThresholdMps { get; set; }
    public int? LapClassifierVersion { get; set; }

    // The full Garmin activity summary as received, stored verbatim and never
    // queried - a hedge so a field not modelled above (training effect,
    // activity training load, max HR, elevation gain, ...) is still
    // recoverable from already-synced rows without re-hitting Garmin. Mirrors
    // DailyWellness.RawJson. Kept out of ActivityDto deliberately: GET
    // /api/activities returns whole date ranges and nothing reads this.
    public string? RawJson { get; set; }

    public List<ActivityLap> Laps { get; set; } = new();

    // The per-second GPS stream, when one has been synced (Ultimate only).
    // Inert unless explicitly Include'd - it carries a ~100 KB blob.
    public ActivityTrack? Track { get; set; }

    // Manual entry: the game's final score from Lachlan's own record, since
    // Garmin has no concept of a team score. Gives "points played" a real
    // denominator. Both null until entered; entered as a pair.
    public int? FinalScoreFor { get; set; }
    public int? FinalScoreAgainst { get; set; }

    public DateTime? DeletedAt { get; set; }
}
