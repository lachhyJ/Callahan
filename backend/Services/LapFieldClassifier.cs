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
// given activity's laps were labelled, so a wrong call is diagnosable later
// without re-running anything.
public static class LapClassifierMethod
{
    // >=2 distinct Garmin work/rest labels present: a structured watch workout,
    // not a manually-lapped game. Mapped directly, no clustering.
    public const string LabelledFromGarmin = "LabelledFromGarmin";
    // Fewer usable laps than MinLapsForSplit - everything on-field.
    public const string TooFewLaps = "TooFewLaps";
    // A split was computed but failed the plausibility guard (e.g. every point
    // played) - everything on-field.
    public const string NoSeparation = "NoSeparation";
    // Split accepted but the speed labelling put every lap on one side (vetoes,
    // or a genuinely lopsided game) - there's no alternation to check.
    public const string AdaptiveSplit = "AdaptiveSplit";
    // Split accepted, both states present, and the speed labels alternate
    // on/off/on/off with no adjacent pair sharing a state. The high-confidence
    // outcome: every transition was lapped.
    public const string AlternatingClean = "AlternatingClean";
    // Split accepted and both states present, but some adjacent laps share a
    // state - a press was missed. Speed labelling is adopted per-lap; runs of
    // adjacent on-field laps get their anomalously-long member flagged Mixed.
    // AlternationViolations records how many adjacent pairs shared a state.
    public const string AlternatingBroken = "AlternatingBroken";
}

// All thresholds live here so retuning after the first real lapped game is a
// one-line change plus a Version bump, followed by POST /api/activities/laps/reclassify.
public sealed record LapClassifierOptions(
    // Below this many usable laps there isn't enough to split on.
    int MinLapsForSplit = 6,
    // The fast cluster must be at least this many times the slow cluster.
    decimal MinCentroidRatio = 2.5m,
    // The slow cluster's mean must be at or below this to be physically a
    // sideline (standing/walking). This absolute anchor - not the ratio - is
    // what stops a drill session (footwork vs sprints) being split.
    decimal MaxPlausibleSidelineSpeed = 1.2m,
    // The fast cluster's mean must be at least this to be play.
    decimal MinPlausiblePointSpeed = 1.5m,
    // The gap at the split boundary must be at least this many pooled SDs.
    double MinGapSds = 1.0,
    // A lap whose max speed reaches this contains a sprint - force on-field.
    decimal SprintVetoMps = 4.0m,
    // A lap whose max speed never reaches this can't be play - force off-field.
    decimal WalkVetoMps = 1.8m,
    // In a broken-alternation run of adjacent on-field laps, the longest is
    // flagged Mixed only if its duration is at least this multiple of the
    // median on-field lap duration.
    decimal MergeDurationFactor = 1.6m)
{
    public static readonly LapClassifierOptions Default = new();
}

public sealed record LapFieldSummary(
    // LapIndex -> LapFieldState. Keyed by index (not object identity) so a
    // reclassify job that loaded laps in any order can still apply it.
    IReadOnlyDictionary<int, string> StateByLapIndex,
    string Method,
    decimal? ThresholdMps,
    int OnFieldSeconds,
    int OffFieldSeconds,
    int MixedSeconds,
    int PointsPlayed,
    decimal OnFieldDistanceM,
    int UnknownLapCount,
    int AlternationViolations);

// Pure. No DbContext, no I/O. This is deliberately callable on any lap list so
// it can be unit-tested in isolation and reused from both the lap-sync path and
// the reclassify endpoint.
public static class LapFieldClassifier
{
    // Bump when the algorithm or default options change in a way that should
    // trigger a reclassify of already-stored activities.
    public const int Version = 1;

    private static readonly HashSet<string> KnownGarminIntensities =
        new(StringComparer.OrdinalIgnoreCase) { "WARMUP", "ACTIVE", "RECOVERY", "REST", "COOLDOWN" };

    private sealed class Lap
    {
        public int Index;
        public double? Speed;   // m/s; null => unusable, stays Unknown
        public int Duration;    // whole seconds, rounded once at intake
        public double Distance; // metres
        public double? MaxSpeed;
        public string? IntensityType;
        public string State = LapFieldState.Unknown;
    }

    public static LapFieldSummary Classify(
        IReadOnlyList<ActivityLap> laps, LapClassifierOptions? options = null)
    {
        var opts = options ?? LapClassifierOptions.Default;

        // --- 0. Normalise. Round duration to whole seconds once here so every
        // downstream sum is exact integer arithmetic. ---
        var all = laps
            .OrderBy(l => l.LapIndex)
            .Select(l =>
            {
                int duration = (int)Math.Round((double)(l.DurationSeconds ?? 0m));
                double distance = (double)(l.DistanceM ?? 0m);
                double? avg = l.AvgSpeedMps is { } a && a > 0m ? (double)a : null;
                double? speed = duration > 0
                    ? avg ?? (distance > 0 ? distance / duration : null)
                    : null;
                return new Lap
                {
                    Index = l.LapIndex,
                    Speed = speed,
                    Duration = duration,
                    Distance = distance,
                    MaxSpeed = l.MaxSpeedMps is { } m && m > 0m ? (double)m : null,
                    IntensityType = l.IntensityType,
                };
            })
            .ToList();

        var usable = all.Where(l => l.Speed is not null).ToList();
        int unknownLapCount = all.Count - usable.Count;

        // --- 1. Trust Garmin when Garmin actually labelled it. ---
        var distinctKnown = usable
            .Select(l => l.IntensityType)
            .Where(t => t is not null && KnownGarminIntensities.Contains(t))
            .Select(t => t!.ToUpperInvariant())
            .Distinct()
            .ToList();
        if (distinctKnown.Count >= 2)
        {
            foreach (var l in usable)
            {
                var t = l.IntensityType?.ToUpperInvariant();
                l.State = t is "ACTIVE" or "WARMUP" ? LapFieldState.OnField : LapFieldState.OffField;
            }
            return Summarise(all, LapClassifierMethod.LabelledFromGarmin, null, 0, unknownLapCount);
        }

        // --- 2. Too small to split. ---
        if (usable.Count < opts.MinLapsForSplit)
        {
            foreach (var l in usable) l.State = LapFieldState.OnField;
            return Summarise(all, LapClassifierMethod.TooFewLaps, null, 0, unknownLapCount);
        }

        // --- 3. Exact optimal 1-D 2-means on ln(speed). Log space because the
        // classes differ multiplicatively (~0.5 vs ~2.5 m/s) and it compresses
        // the one-sided GPS-spike tail. ---
        var bySpeed = usable.OrderBy(l => l.Speed!.Value).ToList();
        int n = bySpeed.Count;
        var x = bySpeed.Select(l => Math.Log(Math.Max(l.Speed!.Value, 0.05))).ToArray();

        var prefix = new double[n + 1];
        var prefixSq = new double[n + 1];
        for (int i = 0; i < n; i++)
        {
            prefix[i + 1] = prefix[i] + x[i];
            prefixSq[i + 1] = prefixSq[i] + x[i] * x[i];
        }
        double Sse(int lo, int hi) // half-open [lo, hi)
        {
            int cnt = hi - lo;
            if (cnt <= 0) return 0;
            double sum = prefix[hi] - prefix[lo];
            double sumSq = prefixSq[hi] - prefixSq[lo];
            // Clamp: for a cluster of identical values this is 0 in exact
            // arithmetic but rounds to a tiny negative, which would poison the
            // sqrt below into NaN.
            return Math.Max(0, sumSq - sum * sum / cnt);
        }

        double bestSse = double.PositiveInfinity;
        int bestK = 1;
        for (int k = 1; k < n; k++)
        {
            double sse = Sse(0, k) + Sse(k, n);
            if (sse < bestSse) { bestSse = sse; bestK = k; }
        }

        double loMean = Math.Exp((prefix[bestK]) / bestK);
        double hiMean = Math.Exp((prefix[n] - prefix[bestK]) / (n - bestK));
        double boundaryGapLog = x[bestK] - x[bestK - 1];
        double pooledSdLog = Math.Sqrt(Math.Max(0, bestSse) / n);

        // --- 4. Guard: relative separation AND absolute plausibility, both
        // required. When it fails, everything is on-field - a game where every
        // point was played is a normal outcome, not an error. ---
        // pooledSdLog == 0 (perfectly tight clusters) is fine, not degenerate:
        // the gap check below becomes `gap >= 0` which always holds, and the
        // ratio + absolute-anchor checks still carry the decision. An
        // all-identical lap list is rejected by the ratio check (loMean == hiMean).
        bool acceptSplit =
            bestK >= 2 &&
            n - bestK >= 2 &&
            hiMean / loMean >= (double)opts.MinCentroidRatio &&
            loMean <= (double)opts.MaxPlausibleSidelineSpeed &&
            hiMean >= (double)opts.MinPlausiblePointSpeed &&
            boundaryGapLog >= opts.MinGapSds * pooledSdLog;

        if (!acceptSplit)
        {
            foreach (var l in usable) l.State = LapFieldState.OnField;
            return Summarise(all, LapClassifierMethod.NoSeparation, null, 0, unknownLapCount);
        }

        double thresholdMps = Math.Sqrt(loMean * hiMean);

        // --- 5. Per-lap speed labelling, then the max-speed vetoes. ---
        var speedState = new Dictionary<int, string>();
        foreach (var l in usable)
        {
            var state = l.Speed!.Value >= thresholdMps ? LapFieldState.OnField : LapFieldState.OffField;
            if (l.MaxSpeed is { } mx)
            {
                if (mx >= (double)opts.SprintVetoMps) state = LapFieldState.OnField;
                else if (mx < (double)opts.WalkVetoMps) state = LapFieldState.OffField;
            }
            speedState[l.Index] = state;
        }

        // --- 6. Alternation check. If every transition was lapped, the speed
        // labels already read on/off/on/off and the phase falls straight out -
        // no separate phase decision needed. Two adjacent laps sharing a state
        // is the signature of a missed press between them. This is a local
        // check: unlike pooling odd/even LapIndex, it survives a single missed
        // press (which inverts the odd/even phase for every lap after it). ---
        bool bothStatesPresent =
            speedState.Values.Contains(LapFieldState.OnField) &&
            speedState.Values.Contains(LapFieldState.OffField);

        if (!bothStatesPresent)
        {
            // Every lap landed on one side despite an accepted split (vetoes,
            // or a lopsided game) - nothing to reconcile.
            ApplyStates(usable, speedState);
            return Summarise(all, LapClassifierMethod.AdaptiveSplit, (decimal)thresholdMps, 0, unknownLapCount);
        }

        int violations = 0;
        for (int k = 1; k < usable.Count; k++)
        {
            if (speedState[usable[k].Index] == speedState[usable[k - 1].Index]) violations++;
        }

        if (violations == 0)
        {
            ApplyStates(usable, speedState);
            return Summarise(all, LapClassifierMethod.AlternatingClean, (decimal)thresholdMps, 0, unknownLapCount);
        }

        // --- 7. Alternation broke. Keep the per-lap speed labelling, then
        // quarantine merged laps: two+ adjacent laps the speed test both calls
        // on-field means a press was missed between them; the anomalously long
        // one in that run gets flagged Mixed (a point welded to a sideline
        // stint is longer than a bare point).
        var finalState = new Dictionary<int, string>(speedState);
        double medianOnFieldDuration = Median(
            usable.Where(l => speedState[l.Index] == LapFieldState.OnField).Select(l => (double)l.Duration));

        int r = 0;
        while (r < usable.Count)
        {
            if (finalState[usable[r].Index] != LapFieldState.OnField) { r++; continue; }
            int j = r;
            while (j < usable.Count && finalState[usable[j].Index] == LapFieldState.OnField) j++;
            if (j - r >= 2 && medianOnFieldDuration > 0)
            {
                var longest = usable.GetRange(r, j - r).OrderByDescending(l => l.Duration).First();
                if (longest.Duration >= (double)opts.MergeDurationFactor * medianOnFieldDuration)
                {
                    finalState[longest.Index] = LapFieldState.Mixed;
                }
            }
            r = j;
        }

        ApplyStates(usable, finalState);
        return Summarise(all, LapClassifierMethod.AlternatingBroken, (decimal)thresholdMps, violations, unknownLapCount);
    }

    private static void ApplyStates(List<Lap> usable, IReadOnlyDictionary<int, string> states)
    {
        foreach (var l in usable) l.State = states[l.Index];
    }

    private static LapFieldSummary Summarise(
        List<Lap> all, string method, decimal? thresholdMps, int alternationViolations, int unknownLapCount)
    {
        int onSec = 0, offSec = 0, mixSec = 0, points = 0;
        decimal onDist = 0m;
        foreach (var l in all)
        {
            switch (l.State)
            {
                case LapFieldState.OnField:
                    onSec += l.Duration;
                    onDist += (decimal)l.Distance;
                    points++;
                    break;
                case LapFieldState.OffField:
                    offSec += l.Duration;
                    break;
                case LapFieldState.Mixed:
                    mixSec += l.Duration;
                    points++;
                    break;
                // Unknown contributes to nothing but UnknownLapCount.
            }
        }

        return new LapFieldSummary(
            all.ToDictionary(l => l.Index, l => l.State),
            method, thresholdMps,
            onSec, offSec, mixSec, points, onDist, unknownLapCount, alternationViolations);
    }

    private static double Median(IEnumerable<double> values)
    {
        var sorted = values.OrderBy(v => v).ToList();
        if (sorted.Count == 0) return 0;
        int mid = sorted.Count / 2;
        return sorted.Count % 2 == 1
            ? sorted[mid]
            : (sorted[mid - 1] + sorted[mid]) / 2;
    }
}
