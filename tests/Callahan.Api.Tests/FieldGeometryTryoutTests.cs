using Callahan.Api.Models;
using Callahan.Api.Services;

namespace Callahan.Api.Tests;

// Ground truth for rest detection: four lap-pressed club tryouts (HoS #2, HoS
// #3, Chilly #3, Ellipsis #2), where he pressed a lap at every sub on and off.
// Laps are labelled by their own speeds (the 2026-11-06 audit), so the
// expected values here come from the laps, not from any detector. Before the
// v5 rewrite the detector called 47-91% of rest time on-field on these.
public class FieldGeometryTryoutTests
{
    public static IEnumerable<object[]> Tryouts =>
        TestFixtures.LoadTryoutBaselines().Select(b => new object[] { b.Tryout });

    private enum Label { Warmup, Point, Rest, Ambiguous }

    // The 2026-11-06 labels. Point is the same rule LapClassifierOptions uses
    // for the fit exclusion; rest/ambiguous only matter to the scoring here.
    private static Label LabelOf(TestFixtures.TryoutLap l) => l switch
    {
        _ when l.DurationSec > 900 => Label.Warmup,
        _ when l.AvgMps >= 0.95 && l.MaxMps >= 5.0 => Label.Point,
        _ when l.AvgMps <= 0.6 && l.MaxMps < 5.0 => Label.Rest,
        _ => Label.Ambiguous,
    };

    private static GeometryResult Analyse(int tryout, out TestFixtures.TryoutLap[] laps, out List<TrackSample> samples)
    {
        var (epoch, s, l, rows) = TestFixtures.LoadTryout(tryout);
        laps = l.ToArray();
        samples = s;
        return LapFieldClassifier.AnalyseWithLaps(rows, s, epoch);
    }

    [Theory]
    [MemberData(nameof(Tryouts))]
    public void RestTimeMostlyReadsOffField(int tryout)
    {
        var r = Analyse(tryout, out var laps, out var samples);

        // He presses a rest lap the moment a point ends, so its first 30 s is
        // legitimately walking off mid-field: score rests without it.
        int restN = 0, restOn = 0;
        foreach (var l in laps.Where(l => LabelOf(l) == Label.Rest))
            for (int i = 0; i < samples.Count; i++)
                if (samples[i].T >= l.StartSec + 30 && samples[i].T < l.StartSec + l.DurationSec)
                {
                    restN++;
                    if (r.OnFieldBySample[i]) restOn++;
                }

        Assert.True(restN > 100, $"too few rest samples to score ({restN})");
        double frac = (double)restOn / restN;
        Assert.True(frac <= 0.20, $"{frac:P0} of rest time read on-field");
    }

    [Theory]
    [MemberData(nameof(Tryouts))]
    public void PointsPlayedAndRestPeriodsTrackTheLaps(int tryout)
    {
        var r = Analyse(tryout, out var laps, out _);

        var labels = laps.Select(LabelOf).ToList();
        int lapPoints = labels.Count(l => l == Label.Point);
        // A rest period is a run of rest/ambiguous laps with a point lap on
        // both sides.
        int lapRests = 0;
        for (int k = 0; k < labels.Count; k++)
        {
            bool restish = labels[k] is Label.Rest or Label.Ambiguous;
            if (!restish || (k > 0 && labels[k - 1] is Label.Rest or Label.Ambiguous)) continue;
            int j = k;
            while (j < labels.Count && labels[j] is Label.Rest or Label.Ambiguous) j++;
            if (k > 0 && labels[k - 1] == Label.Point && j < labels.Count && labels[j] == Label.Point) lapRests++;
        }

        Assert.InRange(r.PointsPlayed, lapPoints - 2, lapPoints + 2);
        Assert.InRange(r.RestPeriods, lapRests - 2, lapRests + 2);

        var b = TestFixtures.LoadTryoutBaselines().Single(x => x.Tryout == tryout);
        Assert.Equal(b.LapPoints, lapPoints);       // the scoring here and the script's agree
        Assert.Equal(b.LapRestPeriods, lapRests);
    }

    // Agreement with the reference implementation on the same input.
    [Theory]
    [MemberData(nameof(Tryouts))]
    public void MatchesReferenceBaseline(int tryout)
    {
        var r = Analyse(tryout, out _, out _);
        var b = TestFixtures.LoadTryoutBaselines().Single(x => x.Tryout == tryout);

        Assert.Equal(b.PointsPlayed, r.PointsPlayed);
        Assert.Equal(b.RestPeriods, r.RestPeriods);
        Assert.InRange(r.GameplayStartT, b.GameplayStartSeconds - 2, b.GameplayStartSeconds + 2);
        Assert.InRange(r.OnFieldSeconds, b.OnFieldSeconds * 0.99, b.OnFieldSeconds * 1.01);
        Assert.InRange(r.LivePlaySeconds, b.LivePlaySeconds * 0.99, b.LivePlaySeconds * 1.01);
        Assert.InRange(2 * r.Fit.HalfWidthM, b.FieldWidthM - 0.2, b.FieldWidthM + 0.2);
        Assert.InRange(2 * r.Fit.HalfLengthM, b.FieldLengthM - 0.2, b.FieldLengthM + 0.2);
    }

    [Fact]
    public void LapPressedGame_ClassifiesFromLaps_WithTheGeometrysCounts()
    {
        var (epoch, samples, _, rows) = TestFixtures.LoadTryout(1);
        var geo = LapFieldClassifier.AnalyseWithLaps(rows, samples, epoch);

        var r = LapFieldClassifier.Classify(rows, geo, samples, epoch);

        Assert.Equal(LapClassifierMethod.GeometryFromLaps, r.Method);
        Assert.Equal(geo.PointsPlayed, r.PointsPlayed);
        Assert.Equal(geo.RestPeriods, r.RestPeriods);
        Assert.Equal(geo.LivePlaySeconds, r.LivePlaySeconds);
    }

    [Fact]
    public void FitExclusion_DropsNonPointLapsOnly()
    {
        var (epoch, samples, laps, rows) = TestFixtures.LoadTryout(1);
        var ex = LapFieldClassifier.FitExclusion(rows, samples, epoch)!;

        bool InLap(TestFixtures.TryoutLap l, int i) => samples[i].T >= l.StartSec && samples[i].T < l.StartSec + l.DurationSec;
        var nonPoint = laps.Where(l => LabelOf(l) != Label.Point).ToList();
        Assert.NotEmpty(nonPoint);
        Assert.Contains(laps, l => LabelOf(l) == Label.Point);
        // Excluded exactly when inside a non-point lap (windows can overlap
        // by a fraction of a second at a press, so a point lap's edge sample
        // may be excluded through its neighbour).
        for (int i = 0; i < samples.Count; i++)
            Assert.Equal(nonPoint.Any(l => InLap(l, i)), ex[i]);
    }

    [Fact]
    public void FitExclusion_TooFewLapsOrNoSpeeds_ExcludesNothing()
    {
        var (epoch, samples, _, rows) = TestFixtures.LoadTryout(1);

        // Garmin's single default lap (or a couple of stray auto-laps) isn't a
        // sub log: an over-900 s lap would otherwise read as a warmup and drop
        // the whole game from the fit.
        Assert.Null(LapFieldClassifier.FitExclusion(rows.Take(3).ToList(), samples, epoch));

        var noSpeeds = rows.Select(l => new ActivityLap
        {
            LapIndex = l.LapIndex, StartTimeGmt = l.StartTimeGmt, DurationSeconds = l.DurationSeconds,
        }).ToList();
        Assert.All(LapFieldClassifier.FitExclusion(noSpeeds, samples, epoch)!, Assert.False);
    }
}
