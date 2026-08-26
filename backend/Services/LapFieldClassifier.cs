using Callahan.Api.Models;

namespace Callahan.Api.Services;

// String constants for ActivityLap.FieldState, following the same
// stringly-typed convention as IntensityType so adding a state later needs no
// migration.
public static class LapFieldState
{
    public const string OnField = "OnField";
    public const string OffField = "OffField";
    public const string Mixed = "Mixed";
    public const string Unknown = "Unknown";
}

// Values written to Activity.LapClassifierMethod - the audit trail for how a
// given activity's laps / segments were labelled, so a wrong call is
// diagnosable later without re-running anything.
public static class LapClassifierMethod
{
    // >=2 distinct Garmin work/rest labels present: a structured watch workout,
    // not a manually-lapped game. Mapped directly, no geometry.
    public const string LabelledFromGarmin = "LabelledFromGarmin";
    // Ultimate Game with no GPS track synced yet. Nothing labelled - speed
    // alone is proven useless for real ultimate, so we do NOT fall back to it.
    public const string NoTrack = "NoTrack";
    // Track present, no usable lap boundaries (never lap-pressed, or no lap
    // carries a StartTimeGmt). On/off-field and points come straight from the
    // geometry segments.
    public const string GeometryNoLaps = "GeometryNoLaps";
    // Track present and laps have boundaries. Each lap is labelled by the
    // fraction of its window the geometry calls on-field (three-way:
    // >=High OnField, <=Low OffField, else Mixed). AlternationViolations counts
    // adjacent same-state laps - a missed press or a mid-point/mid-sideline press.
    public const string GeometryFromLaps = "GeometryFromLaps";

    // --- retired speed-era values. Kept so old Activity rows stay decodable. ---
    [Obsolete("speed-based classifier, removed 2026-08 - real ultimate is too slow for it")]
    public const string TooFewLaps = "TooFewLaps";
    [Obsolete("speed-based classifier, removed 2026-08")]
    public const string NoSeparation = "NoSeparation";
    [Obsolete("speed-based classifier, removed 2026-08")]
    public const string AdaptiveSplit = "AdaptiveSplit";
    [Obsolete("speed-based classifier, removed 2026-08")]
    public const string AlternatingClean = "AlternatingClean";
    [Obsolete("speed-based classifier, removed 2026-08")]
    public const string AlternatingBroken = "AlternatingBroken";
}

// Retuning is a change here + a Version bump + one POST /api/activities/laps/reclassify.
public sealed record LapClassifierOptions(
    // A lap is on-field if the geometry calls at least this fraction of its
    // window on-field, off-field if at most OnFieldFractionLow, else Mixed.
    decimal OnFieldFractionHigh = 0.80m,
    decimal OnFieldFractionLow = 0.20m,
    // A lap whose window holds fewer track samples than this stays Unknown.
    int MinSamplesPerLap = 5,
    // A real per-transition sub log of an Ultimate game has many laps (a
    // player subs off several times over ~20 points). Fewer than this is
    // Garmin's default lap or a stray auto-lap, not a sub log - fall back to
    // GeometryNoLaps, whose segment aggregates are more reliable anyway.
    int MinLapsForBoundaries = 4)
{
    public static readonly LapClassifierOptions Default = new();
}

public sealed record LapFieldSummary(
    // LapIndex -> LapFieldState. Empty for GeometryNoLaps / NoTrack.
    IReadOnlyDictionary<int, string> StateByLapIndex,
    string Method,
    decimal? ThresholdMps,   // always null now - kept for the DTO / old rows
    int OnFieldSeconds,
    int OffFieldSeconds,
    int MixedSeconds,
    int PointsPlayed,
    decimal OnFieldDistanceM,
    int UnknownLapCount,
    int AlternationViolations);

// Pure. No DbContext, no I/O. Callable on any (laps, geometry) pair so it can
// be unit-tested in isolation and reused from lap-sync, track-sync,
// session-type change, and the reclassify endpoint.
public static class LapFieldClassifier
{
    // Bump when the algorithm or option defaults change in a way that should
    // trigger a reclassify of stored activities. v2 = geometry, not speed.
    // v3 = <2 laps falls back to GeometryNoLaps (Garmin's one default lap on an
    // un-lapped game was collapsing OnFieldSeconds to 0).
    // v4 = MinLapsForBoundaries raised to 4 (2 stray auto-laps aren't a sub log).
    public const int Version = 4;

    private static readonly HashSet<string> KnownGarminIntensities =
        new(StringComparer.OrdinalIgnoreCase) { "WARMUP", "ACTIVE", "RECOVERY", "REST", "COOLDOWN" };

    private sealed class Lap
    {
        public int Index;
        public int Duration;          // whole seconds
        public double Distance;       // metres
        public DateTime? StartGmt;
        public string? IntensityType;
        public string State = LapFieldState.Unknown;
    }

    public static LapFieldSummary Classify(
        IReadOnlyList<ActivityLap> laps,
        GeometryResult? geometry,
        IReadOnlyList<TrackSample>? samples,
        long trackStartEpochMs,
        LapClassifierOptions? options = null)
    {
        var opts = options ?? LapClassifierOptions.Default;

        var all = laps
            .OrderBy(l => l.LapIndex)
            .Select(l => new Lap
            {
                Index = l.LapIndex,
                Duration = (int)Math.Round((double)(l.DurationSeconds ?? 0m)),
                Distance = (double)(l.DistanceM ?? 0m),
                StartGmt = l.StartTimeGmt,
                IntensityType = l.IntensityType,
            })
            .ToList();

        // --- 1. Structured watch workout: trust Garmin's own labels. ---
        var distinctKnown = all
            .Select(l => l.IntensityType)
            .Where(t => t is not null && KnownGarminIntensities.Contains(t))
            .Select(t => t!.ToUpperInvariant())
            .Distinct()
            .ToList();
        if (distinctKnown.Count >= 2)
        {
            foreach (var l in all)
            {
                var t = l.IntensityType?.ToUpperInvariant();
                l.State = t is "ACTIVE" or "WARMUP" ? LapFieldState.OnField : LapFieldState.OffField;
            }
            return Summarise(all, LapClassifierMethod.LabelledFromGarmin, alternationViolations: 0,
                pointsOverride: null, distanceFromLaps: true);
        }

        // --- 2. Ultimate Game with no track: nothing to say. Do NOT use speed. ---
        if (geometry is null || samples is null || samples.Count == 0)
        {
            return new LapFieldSummary(
                EmptyStates, LapClassifierMethod.NoTrack, null,
                0, 0, 0, 0, 0m, all.Count, 0);
        }

        var epoch = DateTimeOffset.FromUnixTimeMilliseconds(trackStartEpochMs).UtcDateTime;

        var withWindow = all
            .Where(l => l.StartGmt is not null && l.Duration > 0)
            .ToList();

        // --- 3. Not enough lap boundaries to be a real sub log - fewer than two
        // laps is just Garmin's one default lap for an un-lapped session, which
        // over the whole game reads as a single Mixed lap. Take the aggregates
        // straight from the geometry segments instead. ---
        if (withWindow.Count < opts.MinLapsForBoundaries)
        {
            return new LapFieldSummary(
                EmptyStates, LapClassifierMethod.GeometryNoLaps, null,
                geometry.OnFieldSeconds, geometry.OffFieldSeconds, 0,
                geometry.PointsPlayed, (decimal)geometry.OnFieldDistanceM,
                UnknownLapCount: all.Count, AlternationViolations: 0);
        }

        // --- 4. Label each lap by the on-field fraction of its window. ---
        foreach (var l in all)
        {
            if (l.StartGmt is null || l.Duration <= 0) { l.State = LapFieldState.Unknown; continue; }
            double startRel = (l.StartGmt.Value - epoch).TotalSeconds;
            double frac = FieldGeometry.OnFieldFraction(
                geometry, samples, startRel, startRel + l.Duration, opts.MinSamplesPerLap);
            l.State = frac switch
            {
                < 0 => LapFieldState.Unknown,
                _ when frac >= (double)opts.OnFieldFractionHigh => LapFieldState.OnField,
                _ when frac <= (double)opts.OnFieldFractionLow => LapFieldState.OffField,
                _ => LapFieldState.Mixed,
            };
        }

        // Alternation check: adjacent laps that share a definite (non-Mixed)
        // state - a missed press, or a press made mid-point / mid-sideline.
        int violations = 0;
        var labelled = all.Where(l => l.State is LapFieldState.OnField or LapFieldState.OffField).ToList();
        for (int k = 1; k < labelled.Count; k++)
            if (labelled[k].State == labelled[k - 1].State) violations++;

        // Points come from the geometry's endzone-dwell count, not the lap
        // count - he laps on sub on/off, so one on-field lap can span several
        // points.
        return Summarise(all, LapClassifierMethod.GeometryFromLaps, violations,
            pointsOverride: geometry.PointsPlayed, distanceFromLaps: true);
    }

    private static readonly IReadOnlyDictionary<int, string> EmptyStates =
        new Dictionary<int, string>();

    private static LapFieldSummary Summarise(
        List<Lap> all, string method, int alternationViolations,
        int? pointsOverride, bool distanceFromLaps)
    {
        int onSec = 0, offSec = 0, mixSec = 0, onLapCount = 0, unknown = 0;
        decimal onDist = 0m;
        foreach (var l in all)
        {
            switch (l.State)
            {
                case LapFieldState.OnField:
                    onSec += l.Duration;
                    if (distanceFromLaps) onDist += (decimal)l.Distance;
                    onLapCount++;
                    break;
                case LapFieldState.OffField:
                    offSec += l.Duration;
                    break;
                case LapFieldState.Mixed:
                    mixSec += l.Duration;
                    onLapCount++;
                    break;
                default:
                    unknown++;
                    break;
            }
        }

        return new LapFieldSummary(
            all.ToDictionary(l => l.Index, l => l.State),
            method, null,
            onSec, offSec, mixSec,
            pointsOverride ?? onLapCount, onDist, unknown, alternationViolations);
    }
}
