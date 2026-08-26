using System.IO.Compression;
using System.Reflection;
using System.Text.Json;
using Callahan.Api.Services;

namespace Callahan.Api.Tests;

// The oracle for FieldGeometry is scripts/ultimate-stream-explore/segment.py
// run on the six real AUC D2 games (10-12 Apr 2026). Fixtures are the exact
// wire/storage payload a PUT /api/activities/{id}/track carries, with each
// game's longitudes shifted by a per-game constant (-mean_lon) so no field
// location is in the repo - an output-neutral transform (project() subtracts
// the per-game longitude mean; the cos(latitude) scale is untouched).
//
// Bands, not equality: Math.Atan2 / double ordering / index percentiles won't
// reproduce CPython bit-for-bit, and an exact suite here would be flaky
// forever.
public class FieldGeometryTests
{
    private sealed record Baseline(
        int Game, string Name, int OnFieldSeconds, int DurationSeconds,
        double OnFieldFraction, int PointsPlayed, double FieldWidthM, double FieldLengthM);

    private sealed record Tournament(int OnFieldSeconds, int DurationSeconds, double OnFieldFraction, int PointsPlayed);

    private sealed record Baselines(List<Baseline> Games, Tournament Tournament);

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private static Stream Resource(string name)
    {
        var asm = Assembly.GetExecutingAssembly();
        var full = asm.GetManifestResourceNames().Single(n => n.EndsWith(name, StringComparison.Ordinal));
        return asm.GetManifestResourceStream(full)!;
    }

    private static List<TrackSample> LoadFixture(int game)
    {
        using var gz = new GZipStream(Resource($"game-{game:00}.json.gz"), CompressionMode.Decompress);
        using var doc = JsonDocument.Parse(gz);
        var s = doc.RootElement.GetProperty("samples");
        var t = s.GetProperty("t").EnumerateArray().Select(e => e.GetDouble()).ToArray();
        var lat = s.GetProperty("lat").EnumerateArray().Select(e => e.GetDouble()).ToArray();
        var lon = s.GetProperty("lon").EnumerateArray().Select(e => e.GetDouble()).ToArray();
        var spd = s.GetProperty("spd").EnumerateArray().Select(e => e.GetDouble()).ToArray();
        return Enumerable.Range(0, t.Length).Select(i => new TrackSample(t[i], lat[i], lon[i], spd[i])).ToList();
    }

    private static Baselines LoadBaselines()
    {
        using var r = Resource("baselines.json");
        return JsonSerializer.Deserialize<Baselines>(r, Json)!;
    }

    public static IEnumerable<object[]> Games => Enumerable.Range(1, 6).Select(g => new object[] { g });

    [Theory]
    [MemberData(nameof(Games))]
    public void MatchesSegmentPyBaseline(int game)
    {
        var baseline = LoadBaselines().Games.Single(b => b.Game == game);
        var samples = LoadFixture(game);

        var r = FieldGeometry.Analyse(samples);

        double frac = (double)r.OnFieldSeconds / (r.OnFieldSeconds + r.OffFieldSeconds);
        Assert.InRange(frac, baseline.OnFieldFraction - 0.03, baseline.OnFieldFraction + 0.03);
        Assert.InRange(r.PointsPlayed, baseline.PointsPlayed - 1, baseline.PointsPlayed + 1);

        // duration reconstructed from the track vs segment.py's t[-1]-t[0]
        int dur = r.OnFieldSeconds + r.OffFieldSeconds;
        Assert.InRange(dur, (int)(baseline.DurationSeconds * 0.98), (int)(baseline.DurationSeconds * 1.02));

        Assert.InRange(2 * r.Fit.HalfWidthM, baseline.FieldWidthM * 0.90, baseline.FieldWidthM * 1.10);
        Assert.InRange(2 * r.Fit.HalfLengthM, baseline.FieldLengthM * 0.90, baseline.FieldLengthM * 1.10);

        // A real ultimate field is ~37m wide x ~100m long incl. endzones. The
        // fit is over the whole game including sideline time, so it comes out
        // smaller, but it should still be recognisably a field.
        Assert.InRange(2 * r.Fit.HalfWidthM, 15.0, 55.0);
        Assert.InRange(2 * r.Fit.HalfLengthM, 45.0, 110.0);
    }

    [Fact]
    public void TournamentAggregateWithinBand()
    {
        var b = LoadBaselines().Tournament;
        int on = 0, off = 0, pts = 0;
        foreach (var g in Enumerable.Range(1, 6))
        {
            var r = FieldGeometry.Analyse(LoadFixture(g));
            on += r.OnFieldSeconds; off += r.OffFieldSeconds; pts += r.PointsPlayed;
        }

        double frac = (double)on / (on + off);
        Assert.InRange(frac, b.OnFieldFraction - 0.02, b.OnFieldFraction + 0.02);   // ~0.67
        Assert.InRange(pts, b.PointsPlayed - 4, b.PointsPlayed + 4);                // ~101
        Assert.InRange(on, (int)(b.OnFieldSeconds * 0.97), (int)(b.OnFieldSeconds * 1.03));
    }

    [Fact]
    public void SegmentsCoverTheWholeTrackWithNoGaps()
    {
        var samples = LoadFixture(3);
        var r = FieldGeometry.Analyse(samples);

        Assert.Equal(samples[0].T, r.Segments[0].StartT);
        Assert.Equal(samples[^1].T, r.Segments[^1].EndT);
        for (int i = 1; i < r.Segments.Count; i++)
        {
            Assert.NotEqual(r.Segments[i - 1].OnField, r.Segments[i].OnField);   // runs alternate
            Assert.True(r.Segments[i].StartT >= r.Segments[i - 1].EndT);
        }
    }

    [Fact]
    public void OnFieldFraction_WindowInsideAKnownOnFieldSegment_ReadsHigh()
    {
        var samples = LoadFixture(3);
        var r = FieldGeometry.Analyse(samples);

        // longest on-field segment
        var seg = r.Segments.Where(s => s.OnField).OrderByDescending(s => s.EndT - s.StartT).First();
        double mid = (seg.StartT + seg.EndT) / 2;
        double f = FieldGeometry.OnFieldFraction(r, samples, mid - 20, mid + 20);
        Assert.True(f > 0.8, $"expected mostly on-field, got {f:F2}");
    }

    [Fact]
    public void OnFieldFraction_TooFewSamples_ReturnsNegative()
    {
        var samples = LoadFixture(1);
        var r = FieldGeometry.Analyse(samples);
        Assert.Equal(-1, FieldGeometry.OnFieldFraction(r, samples, 10.0, 12.0));
    }
}
