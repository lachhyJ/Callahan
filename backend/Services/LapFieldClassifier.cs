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

// A lap's kind from its own speeds (LapFieldClassifier.LapKindOf). Display
// and fit-exclusion only; never persisted.
public static class LapKind
{
    public const string Point = "Point";
    public const string Warmup = "Warmup";
    public const string Other = "Other";
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
    int MinLapsForBoundaries = 4,
    // Lap labels for the fit exclusion (FitExclusion): a lap is a point lap
    // when it's no longer than WarmupLapMinSec, averages at least
    // PointLapMinAvgMps and peaks at least PointLapMinMaxMps. Every other lap
    // (warmup, rest, ambiguous) is kept out of the field fit. Mirrors LAP in
    // scripts/ultimate-stream-explore/fieldgeometry_v2.py.
    double WarmupLapMinSec = 900.0,
    double PointLapMinAvgMps = 0.95,
    double PointLapMinMaxMps = 5.0)
{
    public static readonly LapClassifierOptions Default = new();
}

public sealed record LapFieldSummary(
    // LapIndex -> LapFieldState. Empty for GeometryNoLaps / NoTrack.
    IReadOnlyDictionary<int, string> StateByLapIndex,
    string Method,
    int OnFieldSeconds,
    int OffFieldSeconds,
    int MixedSeconds,
    int PointsPlayed,
    decimal OnFieldDistanceM,
    int UnknownLapCount,
    int AlternationViolations,
    // On-field seconds inside a detected point (see GeometryResult.LivePlaySeconds).
    // 0 when there's no geometry (NoTrack) or it doesn't apply (LabelledFromGarmin).
    int LivePlaySeconds = 0,
    // GPS distance (m) covered inside detected points (see
    // GeometryResult.LivePlayDistanceM). 0 in the same cases as LivePlaySeconds.
    decimal LivePlayDistanceM = 0m,
    // Off-field gaps between on-field stints (see GeometryResult.RestPeriods).
    // Null when there's no geometry (NoTrack) or it doesn't apply
    // (LabelledFromGarmin).
    int? RestPeriods = null);

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
    // v5 = FieldGeometry v2 - point-counter follow-on filter relaxed so short
    // points aren't deleted. PointsPlayed rises ~4-13% (see FieldGeometry.cs).
    // v6 = LivePlaySeconds threaded through from FieldGeometry v3. Additive.
    // v7 = LivePlayDistanceM threaded through from FieldGeometry v4. Additive.
    // v8 = FieldGeometry v5 (rewrite); lap-pressed sessions keep warmup / rest /
    // ambiguous laps out of the field fit (FitExclusion). RestPeriods added.
    // v9 = RestPeriods persisted (Activity.RestPeriods). Additive.
    public const int Version = 9;

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
                EmptyStates, LapClassifierMethod.NoTrack,
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
                EmptyStates, LapClassifierMethod.GeometryNoLaps,
                geometry.OnFieldSeconds, geometry.OffFieldSeconds, 0,
                geometry.PointsPlayed, (decimal)geometry.OnFieldDistanceM,
                UnknownLapCount: all.Count, AlternationViolations: 0,
                LivePlaySeconds: geometry.LivePlaySeconds,
                LivePlayDistanceM: (decimal)geometry.LivePlayDistanceM,
                RestPeriods: geometry.RestPeriods);
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

        // Points come from the geometry's stints, not the lap count - he laps
        // on sub on/off, so one on-field lap can span several points.
        return Summarise(all, LapClassifierMethod.GeometryFromLaps, violations,
            pointsOverride: geometry.PointsPlayed, distanceFromLaps: true,
            livePlaySeconds: geometry.LivePlaySeconds,
            livePlayDistanceM: (decimal)geometry.LivePlayDistanceM,
            restPeriods: geometry.RestPeriods);
    }

    // The geometry for a session, with its lap presses (when it has enough to
    // be a real sub log) keeping warmup / rest / ambiguous laps out of the
    // field fit. Laps are labelled by their own speeds, never by position:
    // lap 1 isn't reliably a warmup. A lap with no speeds excludes nothing.
    // Shared by every caller so the stored numbers and the timeline agree.
    // warmupRecorded is Activity.WarmupRecorded: when known, it fixes the
    // gameplay start instead of inferring it (KnownGameplayStart).
    public static GeometryResult AnalyseWithLaps(
        IReadOnlyList<ActivityLap> laps, IReadOnlyList<TrackSample> samples,
        long trackStartEpochMs, LapClassifierOptions? options = null, bool? warmupRecorded = null)
        => FieldGeometry.Analyse(samples,
            excludeFromFit: FitExclusion(laps, samples, trackStartEpochMs, options),
            knownStartT: KnownGameplayStart(laps, trackStartEpochMs, warmupRecorded, options));

    // The gameplay start fixed by the per-activity "warmup recorded?" flag, in
    // seconds from track start, or null to let FieldGeometry infer it.
    // false -> 0 (he went straight in). true -> the pressed warmup lap's end,
    // or null if there isn't one. null (don't know) -> null.
    // Mirrors fieldgeometry_v2.py:known_start.
    public static double? KnownGameplayStart(
        IReadOnlyList<ActivityLap> laps, long trackStartEpochMs, bool? warmupRecorded,
        LapClassifierOptions? options = null)
        => warmupRecorded switch
        {
            false => 0.0,
            true => WarmupLapEnd(laps, trackStartEpochMs, options),
            null => null,
        };

    // End of the pressed warmup lap (seconds from track start), or null. A
    // warmup lap is one longer than WarmupLapMinSec - labelled by duration,
    // never by position, since lap 1 is sometimes sideline time or already
    // gameplay. The earliest wins, and only if another lap starts after it: a
    // single lap over the whole session is Garmin's default lap, not a press.
    // Mirrors fieldgeometry_v2.py:warmup_lap_end.
    public static double? WarmupLapEnd(
        IReadOnlyList<ActivityLap> laps, long trackStartEpochMs, LapClassifierOptions? options = null)
    {
        var opts = options ?? LapClassifierOptions.Default;
        var epoch = DateTimeOffset.FromUnixTimeMilliseconds(trackStartEpochMs).UtcDateTime;
        var windowed = laps
            .Where(l => l.StartTimeGmt is not null && (l.DurationSeconds ?? 0m) > 0m)
            .Select(l => (Start: (l.StartTimeGmt!.Value - epoch).TotalSeconds, Dur: (double)l.DurationSeconds!.Value))
            .OrderBy(l => l.Start)
            .ToList();
        for (int k = 0; k < windowed.Count; k++)
            if (windowed[k].Dur > opts.WarmupLapMinSec)
                return k + 1 < windowed.Count ? windowed[k].Start + windowed[k].Dur : null;
        return null;
    }

    public static bool[]? FitExclusion(
        IReadOnlyList<ActivityLap> laps, IReadOnlyList<TrackSample> samples,
        long trackStartEpochMs, LapClassifierOptions? options = null)
    {
        var opts = options ?? LapClassifierOptions.Default;
        var windowed = laps.Where(l => l.StartTimeGmt is not null && (l.DurationSeconds ?? 0m) > 0m).ToList();
        if (windowed.Count < opts.MinLapsForBoundaries) return null;   // not a sub log: nothing to go on

        var epoch = DateTimeOffset.FromUnixTimeMilliseconds(trackStartEpochMs).UtcDateTime;
        var ex = new bool[samples.Count];
        foreach (var l in windowed)
        {
            if (l.AvgSpeedMps is null || l.MaxSpeedMps is null) continue;
            if (LapKindOf(l, opts) == LapKind.Point) continue;
            double dur = (double)l.DurationSeconds!.Value;
            double start = (l.StartTimeGmt!.Value - epoch).TotalSeconds;
            for (int i = 0; i < samples.Count; i++)
                if (samples[i].T >= start && samples[i].T < start + dur) ex[i] = true;
        }
        return ex;
    }

    // A lap labelled by its own speeds alone, independent of the geometry: the
    // same rule FitExclusion uses. Longer than WarmupLapMinSec is a warmup (or
    // a long sideline stretch); point-paced is a point; anything else (rest, a
    // lap holding a missed press, no speeds) is Other.
    public static string LapKindOf(ActivityLap l, LapClassifierOptions? options = null)
    {
        var opts = options ?? LapClassifierOptions.Default;
        if ((double)(l.DurationSeconds ?? 0m) > opts.WarmupLapMinSec) return LapKind.Warmup;
        if (l.AvgSpeedMps is null || l.MaxSpeedMps is null) return LapKind.Other;
        return (double)l.AvgSpeedMps.Value >= opts.PointLapMinAvgMps
            && (double)l.MaxSpeedMps.Value >= opts.PointLapMinMaxMps
            ? LapKind.Point : LapKind.Other;
    }

    // Laps with a usable window, in time order, or empty when there are too
    // few to be a real sub log (Garmin's default lap, stray auto-laps) - the
    // same gate the classifier uses.
    public static IReadOnlyList<ActivityLap> PressedLaps(
        IReadOnlyList<ActivityLap> laps, LapClassifierOptions? options = null)
    {
        var opts = options ?? LapClassifierOptions.Default;
        var windowed = laps
            .Where(l => l.StartTimeGmt is not null && (l.DurationSeconds ?? 0m) > 0m)
            .OrderBy(l => l.StartTimeGmt)
            .ToList();
        return windowed.Count < opts.MinLapsForBoundaries ? [] : windowed;
    }

    private static readonly IReadOnlyDictionary<int, string> EmptyStates =
        new Dictionary<int, string>();

    private static LapFieldSummary Summarise(
        List<Lap> all, string method, int alternationViolations,
        int? pointsOverride, bool distanceFromLaps, int livePlaySeconds = 0,
        decimal livePlayDistanceM = 0m, int? restPeriods = null)
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
            method,
            onSec, offSec, mixSec,
            pointsOverride ?? onLapCount, onDist, unknown, alternationViolations,
            livePlaySeconds, livePlayDistanceM, restPeriods);
    }
}
