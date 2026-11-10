namespace Callahan.Api.Services;

// One per-second (roughly) GPS sample of an Ultimate game. T is seconds from
// the track's start, Lat/Lon are WGS84 degrees, Spd is Garmin's Doppler
// speed in m/s.
public sealed record TrackSample(double T, double Lat, double Lon, double Spd);

// The per-game field frame. ThetaRad is the long-axis rotation (PCA of fast
// running after the gameplay start); CentreCrossM is the cross-axis offset of
// the field centre in the PCA frame; HalfWidthM is half the fitted width;
// HalfLengthM is centre to endzone line (the box extends EndzoneDepthM past it).
public sealed record FieldFit(double ThetaRad, double CentreCrossM, double HalfWidthM, double HalfLengthM);

// A maximal run of same-state samples. StartT/EndT are the first and last
// sample times of the run (seconds from track start).
public sealed record FieldSegment(bool OnField, double StartT, double EndT);

public sealed record GeometryResult(
    FieldFit Fit,
    IReadOnlyList<bool> OnFieldBySample,   // parallel to the input samples
    IReadOnlyList<FieldSegment> Segments,
    int OnFieldSeconds,
    int OffFieldSeconds,
    // On-field stints, each split at an internal line-up followed by a burst
    // (back-to-back points without leaving the field).
    int PointsPlayed,
    double OnFieldDistanceM,
    // On-field seconds inside a points-played stint, minus line-up dwells -
    // "live play" as opposed to OnFieldSeconds, which also counts waiting on
    // the line. Always <= OnFieldSeconds.
    int LivePlaySeconds,
    // GPS distance over exactly the same samples as LivePlaySeconds. Always
    // <= OnFieldDistanceM.
    double LivePlayDistanceM,
    // Off-field gaps of at least RestMinSec between stints.
    int RestPeriods = 0,
    // Start of gameplay (seconds from track start): known from the warmup
    // flag, else inferred; 0 when no game-like run of line-ups was found.
    // Everything before it is off-field.
    double GameplayStartT = 0);

// Every tunable in scripts/ultimate-stream-explore/fieldgeometry_v2.py (P),
// which asserts on import that it mirrors these defaults. Retuning is a change
// here and there + a FieldGeometry.Version / LapFieldClassifier.Version bump +
// one POST /api/activities/laps/reclassify.
public sealed record FieldGeometryOptions(
    double FastMps = 4.0,             // "definitely playing"; the fit set
    int MinFitSamples = 40,           // fewer fast samples than this: fit on everything
    double ClusterCellM = 10.0,       // main-cluster grid cell
    int ClusterHalfCells = 3,         // ... neighbourhood scored around each cell
    double ClusterRadiusM = 60.0,     // ... and kept around the winner
    double DwellMinSec = 20.0,        // line-up dwell: at least this long ...
    double DwellMaxMeanMps = 1.5,     // ... this slow on average (samples < 2x this) ...
    double DwellFrac = 0.55,          // ... beyond this fraction of the rough half-length
    double BurstSec = 20.0,           // a line-up is followed by a burst within this ...
    double BurstMps = 5.0,            // ... at least this fast
    double LineupGapMinSec = 90.0,    // line-ups closer than this are drill reps
    double LineupGapMaxSec = 480.0,   // a gap longer than this breaks the run
    int LineupRunMin = 3,             // line-ups needed to call gameplay started ...
    int LineupSwitches = 2,           // ... switching ends at least this often ...
    double LineupDensity = 1.2,       // ... with at most this many burst-dwells per run member
    double LineQuantile = 0.5,        // endzone line = this quantile of an end's dwell |along|
    double WidthQuantile = 0.03,      // width = the q .. 1-q span of fast cross
    double EndzoneDepthM = 18.0,      // box extends this far past each line
    double SidePadM = 3.0,            // sideline slack
    double WinSec = 20.0,             // rolling-median window for the box test
    double SlowMaxMps = 3.0,          // a sustained slow stretch has no sample above this ...
    double SlowMinSec = 150.0,        // ... for at least this long => off
    double StintGapSec = 30.0,        // on-field runs closer than this are one stint
    double StintMinSec = 45.0,        // stints shorter than this don't count
    double SplitMinSec = 90.0,        // a stint splits at a line-up at least this long after the last split
    double RestMinSec = 60.0)         // an off gap at least this long between stints is a rest period
{
    public static readonly FieldGeometryOptions Default = new();
}

// Pure. No DbContext, no I/O. A direct port of
// scripts/ultimate-stream-explore/fieldgeometry_v2.py:analyse, which stays the
// reference implementation and the place new ideas are tried. Validated
// against four lap-pressed club tryouts (rest time called on-field 4-15%,
// points played / rest periods within 2 of the laps) and the tournament games;
// see tests/Callahan.Api.Tests/Fixtures.
public static class FieldGeometry
{
    // Bump when the algorithm or the option defaults change in a way that
    // should trigger a reclassify of stored activities.
    // v2 = point-counter follow-on filter relaxed (FollowSec 90->60).
    // v3/v4 = LivePlaySeconds / LivePlayDistanceM added.
    // v5 = rewrite, fitted on the tryout lap presses: fit from the area with
    // the most fast running after an inferred gameplay start, endzone lines
    // from line-up dwells, on-field = a box minus sustained slow stretches (no
    // line-up exemption: inert at SlowMinSec 150, dropped),
    // points played from stints, rest periods added. Points roughly halve and
    // rest time stops reading on-field.
    public const int Version = 5;

    // excludeFromFit: per-sample mask (parallel to samples) of time that must
    // never shape the field fit - warmup / rest / ambiguous laps when the
    // session was lap-pressed. It only affects the fit, not on/off labelling.
    // knownStartT: gameplay start (seconds from track start) when the
    // per-activity "warmup recorded?" flag fixes it (see
    // LapFieldClassifier.KnownGameplayStart); null infers it from line-ups.
    public static GeometryResult Analyse(
        IReadOnlyList<TrackSample> samples, FieldGeometryOptions? options = null,
        IReadOnlyList<bool>? excludeFromFit = null, double? knownStartT = null)
    {
        var o = options ?? FieldGeometryOptions.Default;
        int n = samples.Count;
        var t = new double[n];
        var spd = new double[n];
        for (int i = 0; i < n; i++) { t[i] = samples[i].T; spd[i] = samples[i].Spd; }
        var (x, y) = Project(samples);

        // 1. Fit set: fast samples outside the exclusion, in the main cluster.
        var fast = new List<int>();
        for (int i = 0; i < n; i++)
            if (spd[i] >= o.FastMps && (excludeFromFit is null || !excludeFromFit[i])) fast.Add(i);
        if (fast.Count < o.MinFitSamples)
        {
            fast.Clear();
            for (int i = 0; i < n; i++) if (spd[i] >= o.FastMps) fast.Add(i);
        }
        if (fast.Count < o.MinFitSamples)
        {
            fast.Clear();
            for (int i = 0; i < n; i++) fast.Add(i);
        }
        fast = MainCluster(x, y, fast, o);

        // 2. Gameplay start: fit -> start -> refit on post-start play, until it settles.
        var fit = fast;
        int? g0n = null;
        if (knownStartT is double ks)   // known: the first sample at or after it
        {
            g0n = n - 1;
            for (int i = 0; i < n; i++) if (t[i] >= ks) { g0n = i; break; }
        }
        for (int iter = 0; iter < (knownStartT is null ? 4 : 0); iter++)
        {
            var (ox0, oy0, th0) = Pca(x, y, fit);
            var (a0, _) = Frame(x, y, ox0, oy0, th0);
            double rh0 = RoughHalfLength(a0, fit);
            int ng = GameplayStart(t, Dwells(t, a0, spd, rh0 * o.DwellFrac, o), spd, o);
            if (ng == g0n) break;
            g0n = ng;
            var nf = fit.Where(i => i >= ng).ToList();
            if (nf.Count < o.MinFitSamples) break;
            fit = nf;
        }
        int g0 = g0n ?? 0;

        // 3. Geometry from fast play after the start.
        var after = fast.Where(i => i >= g0).ToList();
        fit = after.Count >= o.MinFitSamples ? after : fast;
        var (ox, oy, th) = Pca(x, y, fit);
        var (along, cross) = Frame(x, y, ox, oy, th);
        double rh = RoughHalfLength(along, fit);
        var dw = Dwells(t, along, spd, rh * o.DwellFrac, o).Where(d => d.I0 >= g0).ToList();

        // Endzone lines: the LineQuantile of each end's line-up |along|, then
        // re-centred so the two are symmetric.
        double LineFor(int side)
        {
            var v = new List<double>();
            foreach (var d in dw)
                if (d.Side == side)
                    for (int i = d.I0; i <= d.I1; i++) v.Add(Math.Abs(along[i]));
            return v.Count > 10 ? Percentile(v, o.LineQuantile) : rh;
        }
        double linePos = LineFor(1), lineNeg = LineFor(-1);
        double shift = (linePos - lineNeg) / 2;
        for (int i = 0; i < n; i++) along[i] -= shift;
        double line = (linePos + lineNeg) / 2;

        // Width: the WidthQuantile .. 1-WidthQuantile span of fast cross,
        // centred between them (not on the median, which follows the side he
        // favours).
        var fc = fit.Select(i => cross[i]).ToList();
        fc.Sort();
        double lo = fc[(int)(fc.Count * o.WidthQuantile)];
        double hi = fc[(int)(fc.Count * (1 - o.WidthQuantile))];
        double c0 = (lo + hi) / 2;
        for (int i = 0; i < n; i++) cross[i] -= c0;
        double halfW = (hi - lo) / 2;

        // 4. On-field.
        var onField = OnField(t, along, cross, spd, halfW, line, g0, o);

        // 5. Stints, points played, rest periods.
        var runs = new List<(int A, int B)>();
        for (int i = g0; i < n;)
        {
            if (!onField[i]) { i++; continue; }
            int j = i;
            while (j + 1 < n && onField[j + 1]) j++;
            if (runs.Count > 0 && t[i] - t[runs[^1].B] < o.StintGapSec)
                runs[^1] = (runs[^1].A, j);
            else
                runs.Add((i, j));
            i = j + 1;
        }
        var stints = runs.Where(r => t[r.B] - t[r.A] >= o.StintMinSec).ToList();

        int played = 0;
        foreach (var (a, b) in stints)
        {
            double last = t[a];
            int k = 1;
            foreach (var d in dw)
            {
                if (d.I0 >= a && d.I0 <= b && t[d.I0] - last >= o.SplitMinSec
                    && BurstAfter(t, spd, d.I1, includeSelf: true, o))
                {
                    k++;
                    last = t[d.I0];
                }
            }
            played += k;
        }
        int rests = 0;
        for (int k = 1; k < stints.Count; k++)
            if (t[stints[k].A] - t[stints[k - 1].B] >= o.RestMinSec) rests++;

        // Live play: on-field inside a stint, minus line-up dwells.
        var live = new bool[n];
        foreach (var (a, b) in stints) for (int i = a; i <= b; i++) live[i] = true;
        foreach (var d in dw) for (int i = d.I0; i <= d.I1; i++) live[i] = false;

        double onSec = 0, offSec = 0, onDist = 0, liveSec = 0, liveDist = 0;
        for (int i = 1; i < n; i++)
        {
            double dt = t[i] - t[i - 1];
            if (!onField[i - 1]) { offSec += dt; continue; }
            double dd = Haversine(samples[i - 1], samples[i]);
            onSec += dt;
            onDist += dd;
            if (live[i - 1]) { liveSec += dt; liveDist += dd; }
        }

        var segments = new List<FieldSegment>();
        foreach (var (state, i0, i1) in Runs(onField))
            segments.Add(new FieldSegment(state, t[i0], t[i1]));

        var fieldFit = new FieldFit(th, c0, halfW, line);
        return new GeometryResult(fieldFit, onField, segments,
            (int)Math.Round(onSec), (int)Math.Round(offSec), played, onDist,
            (int)Math.Round(liveSec), liveDist, rests, n > 0 ? t[g0] - t[0] : 0);
    }

    // Fraction of the time window [t0, t1) that was on-field, weighted by the
    // gap to the next sample. Returns -1 when the window holds too few samples
    // to judge (caller treats that as Unknown).
    public static double OnFieldFraction(
        GeometryResult r, IReadOnlyList<TrackSample> samples, double t0, double t1, int minSamples = 5)
    {
        int inWindow = 0;
        double total = 0, on = 0;
        for (int i = 0; i < samples.Count; i++)
        {
            double ti = samples[i].T;
            if (ti < t0 || ti >= t1) continue;
            inWindow++;
            if (i + 1 < samples.Count)
            {
                double dt = samples[i + 1].T - ti;
                total += dt;
                if (r.OnFieldBySample[i]) on += dt;
            }
        }
        if (inWindow < minSamples || total <= 0) return -1;
        return on / total;
    }

    // --- fieldgeometry_v2.py:xy ---
    private static (double[] X, double[] Y) Project(IReadOnlyList<TrackSample> s)
    {
        int n = s.Count;
        double mla = 0, mlo = 0;
        for (int i = 0; i < n; i++) { mla += s[i].Lat; mlo += s[i].Lon; }
        mla /= n; mlo /= n;
        const double k = 111320.0;
        double kLon = k * Math.Cos(mla * Math.PI / 180.0);
        var x = new double[n];
        var y = new double[n];
        for (int i = 0; i < n; i++)
        {
            x[i] = (s[i].Lon - mlo) * kLon;
            y[i] = (s[i].Lat - mla) * k;
        }
        return (x, y);
    }

    private static (double[] Along, double[] Cross) Frame(double[] x, double[] y, double ox, double oy, double th)
    {
        double c = Math.Cos(th), s = Math.Sin(th);
        int n = x.Length;
        var along = new double[n];
        var cross = new double[n];
        for (int i = 0; i < n; i++)
        {
            double dx = x[i] - ox, dy = y[i] - oy;
            along[i] = dx * c + dy * s;
            cross[i] = -dx * s + dy * c;
        }
        return (along, cross);
    }

    // --- fieldgeometry_v2.py:pca --- centroid and principal-axis angle.
    private static (double Ox, double Oy, double Theta) Pca(double[] x, double[] y, List<int> idx)
    {
        double ox = 0, oy = 0;
        foreach (int i in idx) { ox += x[i]; oy += y[i]; }
        ox /= idx.Count; oy /= idx.Count;
        double cxx = 0, cyy = 0, cxy = 0;
        foreach (int i in idx)
        {
            cxx += (x[i] - ox) * (x[i] - ox);
            cyy += (y[i] - oy) * (y[i] - oy);
            cxy += (x[i] - ox) * (y[i] - oy);
        }
        return (ox, oy, 0.5 * Math.Atan2(2 * cxy, cxx - cyy));
    }

    private static double RoughHalfLength(double[] along, List<int> fit)
        => Percentile(fit.Select(i => Math.Abs(along[i])).ToList(), 0.9);

    // --- fieldgeometry_v2.py:main_cluster --- members of idx near the place
    // with the most fast running, so a warmup on another field (far less fast
    // time than the games) can't drag the fit. Cells are scored in first-seen
    // order and the first maximum wins, matching Python's max() over a Counter.
    private static List<int> MainCluster(double[] x, double[] y, List<int> idx, FieldGeometryOptions o)
    {
        double cell = o.ClusterCellM;
        int half = o.ClusterHalfCells;
        var counts = new Dictionary<(int, int), int>();
        var order = new List<(int, int)>();
        foreach (int i in idx)
        {
            var key = ((int)Math.Floor(x[i] / cell), (int)Math.Floor(y[i] / cell));
            if (counts.TryGetValue(key, out int c)) counts[key] = c + 1;
            else { counts[key] = 1; order.Add(key); }
        }
        (int Cx, int Cy) best = order[0];
        int bestScore = -1;
        foreach (var (cx, cy) in order)
        {
            int score = 0;
            for (int a = -half; a <= half; a++)
                for (int b = -half; b <= half; b++)
                    if (counts.TryGetValue((cx + a, cy + b), out int c)) score += c;
            if (score > bestScore) { best = (cx, cy); bestScore = score; }
        }
        double bx = (best.Cx + 0.5) * cell, by = (best.Cy + 0.5) * cell;
        var near = idx.Where(i => Hypot(x[i] - bx, y[i] - by) <= o.ClusterRadiusM).ToList();
        double mx = near.Average(i => x[i]), my = near.Average(i => y[i]);
        return idx.Where(i => Hypot(x[i] - mx, y[i] - my) <= o.ClusterRadiusM).ToList();
    }

    // A >= BurstMps sample within BurstSec after sample i1 (optionally counting i1 itself).
    private static bool BurstAfter(double[] t, double[] spd, int i1, bool includeSelf, FieldGeometryOptions o)
    {
        for (int k = includeSelf ? i1 : i1 + 1; k < t.Length && t[k] - t[i1] <= o.BurstSec; k++)
            if (spd[k] >= o.BurstMps) return true;
        return false;
    }

    // --- fieldgeometry_v2.py:dwells --- slow stretches beyond loAbs along the
    // axis, each kept within one end: (I0, I1, Side).
    private static List<(int I0, int I1, int Side)> Dwells(
        double[] t, double[] along, double[] spd, double loAbs, FieldGeometryOptions o)
    {
        var outv = new List<(int, int, int)>();
        int n = t.Length;
        double cap = o.DwellMaxMeanMps * 2;
        int i = 0;
        while (i < n)
        {
            if (Math.Abs(along[i]) <= loAbs) { i++; continue; }
            int side = along[i] > 0 ? 1 : -1;
            int j = i;
            while (j + 1 < n && Math.Abs(along[j + 1]) > loAbs && (along[j + 1] > 0) == (side > 0)) j++;
            int k = i;
            while (k <= j)
            {
                if (spd[k] >= cap) { k++; continue; }
                int m = k;
                while (m + 1 <= j && spd[m + 1] < cap) m++;
                if (t[m] - t[k] >= o.DwellMinSec)
                {
                    double mean = 0;
                    for (int q = k; q <= m; q++) mean += spd[q];
                    mean /= m - k + 1;
                    if (mean <= o.DwellMaxMeanMps) outv.Add((k, m, side));
                }
                k = m + 1;
            }
            i = j + 1;
        }
        return outv;
    }

    // --- fieldgeometry_v2.py:gameplay_start --- index of the first line-up of a
    // run that looks like games, not drills: each followed by a burst,
    // consecutive ones LineupGapMin..Max apart (start to start), switching ends
    // at least LineupSwitches times, and not dense with drill reps in between.
    // 0 when no run qualifies. Deliberately never keyed on lap position.
    private static int GameplayStart(double[] t, List<(int I0, int I1, int Side)> dw, double[] spd, FieldGeometryOptions o)
    {
        var g = dw.Where(d => BurstAfter(t, spd, d.I1, includeSelf: false, o)).ToList();
        for (int a = 0; a < g.Count; a++)
        {
            var run = new List<(int I0, int I1, int Side)> { g[a] };
            for (int b = a + 1; b < g.Count; b++)
            {
                double gap = t[g[b].I0] - t[run[^1].I0];
                if (gap < o.LineupGapMinSec) continue;   // drill rep: skip, don't break the run
                if (gap > o.LineupGapMaxSec) break;
                run.Add(g[b]);
                int switches = 0;
                for (int q = 1; q < run.Count; q++) if (run[q].Side != run[q - 1].Side) switches++;
                if (run.Count >= o.LineupRunMin && switches >= o.LineupSwitches)
                {
                    double from = t[run[0].I0], to = t[run[^1].I0];
                    int inside = g.Count(d => t[d.I0] >= from && t[d.I0] <= to);
                    if (inside <= run.Count * o.LineupDensity) return g[a].I0;
                    break;
                }
            }
        }
        return 0;
    }

    // --- fieldgeometry_v2.py:onfield --- inside the box on a WinSec rolling
    // median, minus sustained slow stretches, and never before the gameplay
    // start. A line-up is far shorter than SlowMinSec, so needs no exemption.
    private static bool[] OnField(double[] t, double[] along, double[] cross, double[] spd,
        double halfW, double line, int g0, FieldGeometryOptions o)
    {
        int n = t.Length;
        var absCross = new double[n];
        for (int i = 0; i < n; i++) absCross[i] = Math.Abs(cross[i]);
        var mc = Roll(t, absCross, o.WinSec, Median);
        var ma = Roll(t, along, o.WinSec, Median);
        double lim = line + o.EndzoneDepthM;

        var on = new bool[n];
        for (int i = 0; i < n; i++) on[i] = mc[i] < halfW + o.SidePadM && ma[i] > -lim && ma[i] < lim;

        for (int i = 0; i < n;)
        {
            if (spd[i] >= o.SlowMaxMps) { i++; continue; }
            int j = i;
            while (j + 1 < n && spd[j + 1] < o.SlowMaxMps) j++;
            if (t[j] - t[i] >= o.SlowMinSec)
                for (int k = i; k <= j; k++) on[k] = false;
            i = j + 1;
        }
        for (int i = 0; i < g0; i++) on[i] = false;
        return on;
    }

    // --- primitives, ported to match Python semantics exactly ---

    // Two-pointer window over IRREGULAR timestamps. Do not replace with a
    // fixed-N window.
    private static double[] Roll(double[] t, double[] vals, double win, Func<List<double>, double> fn)
    {
        int n = t.Length;
        var outv = new double[n];
        int lo = 0, hi = 0;
        var buf = new List<double>();
        for (int i = 0; i < n; i++)
        {
            while (t[lo] < t[i] - win / 2) lo++;
            if (hi < lo) hi = lo;
            while (hi < n && t[hi] <= t[i] + win / 2) hi++;
            buf.Clear();
            if (hi > lo) for (int k = lo; k < hi; k++) buf.Add(vals[k]);
            else buf.Add(vals[i]);
            outv[i] = fn(buf);
        }
        return outv;
    }

    // Index-based percentile matching Python's `sorted(v)[int(n*p)]`.
    private static double Percentile(List<double> xs, double p)
    {
        var s = new List<double>(xs);
        s.Sort();
        return s[(int)(s.Count * p)];
    }

    private static IEnumerable<(bool State, int I0, int I1)> Runs(bool[] lab)
    {
        int i = 0, n = lab.Length;
        while (i < n)
        {
            int j = i;
            while (j < n && lab[j] == lab[i]) j++;
            yield return (lab[i], i, j - 1);
            i = j;
        }
    }

    // Python statistics.median: sorted; even -> mean of the two middle.
    private static double Median(List<double> values)
    {
        if (values.Count == 0) return 0;
        var s = new List<double>(values);
        s.Sort();
        int mid = s.Count / 2;
        return s.Count % 2 == 1 ? s[mid] : (s[mid - 1] + s[mid]) / 2.0;
    }

    private static double Hypot(double a, double b) => Math.Sqrt(a * a + b * b);

    private static double Haversine(TrackSample a, TrackSample b)
    {
        const double r = 6371000.0;
        double p1 = a.Lat * Math.PI / 180.0, p2 = b.Lat * Math.PI / 180.0;
        double dp = (b.Lat - a.Lat) * Math.PI / 180.0;
        double dl = (b.Lon - a.Lon) * Math.PI / 180.0;
        double h = Math.Sin(dp / 2) * Math.Sin(dp / 2)
                   + Math.Cos(p1) * Math.Cos(p2) * Math.Sin(dl / 2) * Math.Sin(dl / 2);
        return 2 * r * Math.Asin(Math.Min(1.0, Math.Sqrt(h)));
    }
}
