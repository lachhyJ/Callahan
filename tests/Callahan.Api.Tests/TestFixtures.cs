using System.IO.Compression;
using System.Reflection;
using System.Text.Json;
using Callahan.Api.Services;

namespace Callahan.Api.Tests;

// The six real AUC D2 games (10-12 Apr 2026) as the exact wire/storage payload
// of PUT /api/activities/{id}/track, longitudes shifted by a per-game constant
// so no field location is in the repo (output-neutral: project() subtracts the
// per-game longitude mean).
internal static class TestFixtures
{
    public sealed record Baseline(
        int Game, string Name, int OnFieldSeconds, int DurationSeconds,
        double OnFieldFraction, int PointsPlayed, double FieldWidthM, double FieldLengthM,
        int LivePlaySeconds, int LivePlayDistanceM);

    public sealed record Tournament(
        int OnFieldSeconds, int DurationSeconds, double OnFieldFraction, int PointsPlayed,
        int LivePlaySeconds, int LivePlayDistanceM);

    public sealed record Baselines(List<Baseline> Games, Tournament Tournament);

    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    private static Stream Resource(string endsWith)
    {
        var asm = Assembly.GetExecutingAssembly();
        var name = asm.GetManifestResourceNames().Single(n => n.EndsWith(endsWith, StringComparison.Ordinal));
        return asm.GetManifestResourceStream(name)!;
    }

    // The raw {startEpochMs, samples:{t,lat,lon,spd}} payload.
    public static (long StartEpochMs, List<TrackSample> Samples) LoadTrack(int game)
    {
        using var gz = new GZipStream(Resource($"game-{game:00}.json.gz"), CompressionMode.Decompress);
        using var doc = JsonDocument.Parse(gz);
        long start = doc.RootElement.GetProperty("startEpochMs").GetInt64();
        var s = doc.RootElement.GetProperty("samples");
        var t = s.GetProperty("t").EnumerateArray().Select(e => e.GetDouble()).ToArray();
        var lat = s.GetProperty("lat").EnumerateArray().Select(e => e.GetDouble()).ToArray();
        var lon = s.GetProperty("lon").EnumerateArray().Select(e => e.GetDouble()).ToArray();
        var spd = s.GetProperty("spd").EnumerateArray().Select(e => e.GetDouble()).ToArray();
        var samples = Enumerable.Range(0, t.Length)
            .Select(i => new TrackSample(t[i], lat[i], lon[i], spd[i])).ToList();
        return (start, samples);
    }

    public static Baselines LoadBaselines()
    {
        using var r = Resource("baselines.json");
        return JsonSerializer.Deserialize<Baselines>(r, Web)!;
    }
}
