namespace Callahan.Api.Services;

// One per-second (roughly) GPS sample of an Ultimate game. T is seconds from
// the track's start, Lat/Lon are WGS84 degrees, Spd is Garmin's Doppler
// speed in m/s.
public sealed record TrackSample(double T, double Lat, double Lon, double Spd);

// The per-game field frame, fitted from the >= FastMps samples (which are
// unambiguously on-field play). Theta is the long-axis rotation; the rest are
// metres in the fitted frame.
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
    int PointsPlayed,
    double OnFieldDistanceM);

// Every magic number from scripts/ultimate-stream-explore/segment.py, one
// constant each. Retuning geometry is a change here + a FieldGeometry.Version
// bump + one POST /api/activities/laps/reclassify.
public sealed record FieldGeometryOptions(
    double WinSec = 100.0,          // rolling-window width for the on-field features
    double FastMps = 4.0,           // "unambiguously playing" - used to fit the frame
    double MinDwellSec = 75.0,      // segments shorter than this are merged away
    double SpreadFactor = 0.8,      // on-field if lateral spread > HalfWidth * this ...
    double CentreFactor = 0.55,     // ... OR median |cross| < HalfWidth * this
    double HalfWidthPct = 0.90,     // percentile of |cross|/|along| over fast samples
    double EndzoneFrac = 0.55,      // |along| beyond HalfLength * this = an endzone
    double EndzoneMinSec = 25.0,    // an endzone dwell must last this long ...
    double EndzoneMaxSpd = 2.5,     // ... and be this slow on average to be a reset
    double FollowSec = 60.0,        // after a reset, look this far ahead ...
    double FollowFrac = 0.5)        // ... and require this much of it to be on-field
{
    public static readonly FieldGeometryOptions Default = new();
}

// Pure. No DbContext, no I/O. A direct port of segment.py, validated against
// six real games (see tests/Callahan.Api.Tests/Fixtures). The Python file
// stays as the reference implementation and the place new ideas are tried.
public static class FieldGeometry
{
    // Bump when the algorithm or the option defaults change in a way that
    // should trigger a reclassify of stored activities.
    // v2 = the follow-on filter relaxed (FollowSec 90->60, FollowFrac 0.6->0.5)
    // so short D points where the opposition scores fast and he subs off stop
    // being deleted. Held-out validated on the 11 Feb/Mar games. The reclassify
    // gate keys on LapFieldClassifier.Version, bumped in lockstep.
    public const int Version = 2;

    public static GeometryResult Analyse(IReadOnlyList<TrackSample> samples, FieldGeometryOptions? options = null)
    {
        var o = options ?? FieldGeometryOptions.Default;
        int n = samples.Count;
        var t = new double[n];
        var spd = new double[n];
        for (int i = 0; i < n; i++) { t[i] = samples[i].T; spd[i] = samples[i].Spd; }

        var (along, cross, fit0) = Project(samples, o);

        // Field half-dimensions: the HalfWidthPct percentile of |cross| / |along|
        // over the fast (definitely-playing) samples. Falls back to fixed
        // guesses when there aren't enough fast samples to fit on.
        var fastCross = new List<double>();
        var fastAlong = new List<double>();
        for (int i = 0; i < n; i++)
        {
            if (spd[i] >= o.FastMps)
            {
                fastCross.Add(Math.Abs(cross[i]));
                fastAlong.Add(Math.Abs(along[i]));
            }
        }
        double halfW = fastCross.Count > 20 ? Percentile(fastCross, o.HalfWidthPct) : 18.0;
        double halfL = fastAlong.Count > 20 ? Percentile(fastAlong, o.HalfWidthPct) : 45.0;

        // On-field feature: wide lateral spread in the window OR sitting near
        // the centre line. Then absorb runs shorter than MinDwellSec.
        var absCross = new double[n];
        for (int i = 0; i < n; i++) absCross[i] = Math.Abs(cross[i]);
        var latSpread = Roll(t, cross, o.WinSec, Spread);
        var medAbsCross = Roll(t, absCross, o.WinSec, Median);

        var onRaw = new bool[n];
        for (int i = 0; i < n; i++)
            onRaw[i] = latSpread[i] > halfW * o.SpreadFactor || medAbsCross[i] < halfW * o.CentreFactor;
        var onField = MergeShort(t, onRaw, o.MinDwellSec);

        int points = CountPoints(t, along, spd, onField, halfL, o);

        double onSec = 0, offSec = 0, onDist = 0;
        for (int i = 1; i < n; i++)
        {
            double dt = t[i] - t[i - 1];
            if (onField[i - 1])
            {
                onSec += dt;
                onDist += Haversine(samples[i - 1], samples[i]);
            }
            else
            {
                offSec += dt;
            }
        }

        var segments = new List<FieldSegment>();
        foreach (var (state, i0, i1) in Runs(onField))
            segments.Add(new FieldSegment(state, t[i0], t[i1]));

        var fit = new FieldFit(fit0.ThetaRad, fit0.CentreCrossM, halfW, halfL);
        return new GeometryResult(fit, onField, segments,
            (int)Math.Round(onSec), (int)Math.Round(offSec), points, onDist);
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

    // --- port of segment.py:project ---
    private static (double[] Along, double[] Cross, FieldFit Fit) Project(
        IReadOnlyList<TrackSample> s, FieldGeometryOptions o)
    {
        int n = s.Count;
        double mla = 0, mlo = 0;
        for (int i = 0; i < n; i++) { mla += s[i].Lat; mlo += s[i].Lon; }
        mla /= n; mlo /= n;
        double mLatM = 111320.0;
        double mLonM = 111320.0 * Math.Cos(mla * Math.PI / 180.0);

        var x = new double[n];
        var y = new double[n];
        for (int i = 0; i < n; i++)
        {
            x[i] = (s[i].Lon - mlo) * mLonM;
            y[i] = (s[i].Lat - mla) * mLatM;
        }

        // Frame centre + rotation from the fast-sample covariance (fall back to
        // all samples when fewer than 40 are fast).
        var srcIdx = new List<int>();
        for (int i = 0; i < n; i++) if (s[i].Spd >= o.FastMps) srcIdx.Add(i);
        if (srcIdx.Count < 40) { srcIdx.Clear(); for (int i = 0; i < n; i++) srcIdx.Add(i); }

        double mx = 0, my = 0;
        foreach (int i in srcIdx) { mx += x[i]; my += y[i]; }
        mx /= srcIdx.Count; my /= srcIdx.Count;

        double cxx = 0, cyy = 0, cxy = 0;
        foreach (int i in srcIdx)
        {
            cxx += (x[i] - mx) * (x[i] - mx);
            cyy += (y[i] - my) * (y[i] - my);
            cxy += (x[i] - mx) * (y[i] - my);
        }
        cxx /= srcIdx.Count; cyy /= srcIdx.Count; cxy /= srcIdx.Count;

        double th = 0.5 * Math.Atan2(2 * cxy, cxx - cyy);
        double ct = Math.Cos(th), st = Math.Sin(th);

        var along = new double[n];
        var cross = new double[n];
        for (int i = 0; i < n; i++)
        {
            double dx = x[i] - mx, dy = y[i] - my;
            along[i] = dx * ct + dy * st;
            cross[i] = -dx * st + dy * ct;
        }

        // Zero the cross axis on the median of the fast samples (fall back to
        // all when fewer than 40 are fast).
        var fc = new List<double>();
        for (int i = 0; i < n; i++) if (s[i].Spd >= o.FastMps) fc.Add(cross[i]);
        double c0 = fc.Count >= 40 ? Median(fc) : Median(new List<double>(cross));
        for (int i = 0; i < n; i++) cross[i] -= c0;

        return (along, cross, new FieldFit(th, c0, 0, 0));
    }

    // --- port of segment.py point counter (non-strict variant) ---
    private static int CountPoints(
        double[] t, double[] along, double[] spd, bool[] onField, double halfL, FieldGeometryOptions o)
    {
        int n = t.Length;
        var inEz = new bool[n];
        for (int i = 0; i < n; i++) inEz[i] = Math.Abs(along[i]) > halfL * o.EndzoneFrac;

        int pts = 0;
        foreach (var (state, i0, i1) in Runs(inEz))
        {
            if (!state) continue;
            if (t[i1] - t[i0] < o.EndzoneMinSec) continue;

            double meanSpd = 0;
            for (int i = i0; i <= i1; i++) meanSpd += spd[i];
            meanSpd /= (i1 - i0 + 1);
            if (meanSpd > o.EndzoneMaxSpd) continue;

            int onCount = 0;
            for (int i = i0; i <= i1; i++) if (onField[i]) onCount++;
            if (onCount < (i1 - i0 + 1) / 2.0) continue;   // dwell happened while benched

            // Did he actually play the point that followed? Require the next
            // FollowSec to be mostly on-field. Catches points started
            // stationary (deep zone D, handler on O) that a pull-sprint test
            // would miss, and rejects sitting on the line for instructions
            // then returning to the sideline.
            int j = i1;
            while (j < n - 1 && t[j] - t[i1] < o.FollowSec) j++;
            int followLen = j - i1 + 1;
            int followOn = 0;
            for (int i = i1; i <= j; i++) if (onField[i]) followOn++;
            if (followLen == 0 || followOn < followLen * o.FollowFrac) continue;

            pts++;
        }
        return pts;
    }

    // --- primitives, ported to match Python semantics exactly ---

    // segment.py:roll - two-pointer window over IRREGULAR timestamps. Do not
    // replace with a fixed-N window.
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
            if (hi > lo)
            {
                buf.Clear();
                for (int k = lo; k < hi; k++) buf.Add(vals[k]);
                outv[i] = fn(buf);
            }
            else
            {
                outv[i] = fn(new List<double> { vals[i] });
            }
        }
        return outv;
    }

    // segment.py:spread - index-based 10/90 percentile gap (NOT interpolated).
    private static double Spread(List<double> xs)
    {
        if (xs.Count < 4) return 0.0;
        var s = new List<double>(xs);
        s.Sort();
        int n = s.Count;
        return s[(int)(n * 0.9)] - s[(int)(n * 0.1)];
    }

    // Index-based percentile matching Python's `sorted(v)[int(n*p)]`.
    private static double Percentile(List<double> xs, double p)
    {
        var s = new List<double>(xs);
        s.Sort();
        return s[(int)(s.Count * p)];
    }

    // segment.py:merge - absorb runs shorter than mind into the preceding run.
    // The i=0 restart after a merge is an intentional fixpoint iteration; keep
    // it, do not flatten to a single pass.
    private static bool[] MergeShort(double[] t, bool[] lab, double mind)
    {
        var outv = (bool[])lab.Clone();
        int i = 0, n = outv.Length;
        while (i < n)
        {
            int j = i;
            while (j < n && outv[j] == outv[i]) j++;
            if (j > i && t[j - 1] - t[i] < mind && i > 0)
            {
                for (int k = i; k < j; k++) outv[k] = outv[i - 1];
                i = 0;
            }
            else
            {
                i = j;
            }
        }
        return outv;
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
