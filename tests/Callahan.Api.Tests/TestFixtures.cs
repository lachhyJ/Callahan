using System.IO.Compression;
using System.Reflection;
using System.Text.Json;
using Callahan.Api.Models;
using Callahan.Api.Services;

namespace Callahan.Api.Tests;

// The real tournament games (17 across three tournaments; see baselines.json) as the exact wire/storage payload
// of PUT /api/activities/{id}/track, with the location and date blurred (scripts/ultimate-stream-explore/anonymise.py):
//  - per game, latitude moved to a random band far from the real one and longitude re-centred on 0, with the
//    longitude offsets rescaled by cos(old lat)/cos(new lat) so project()'s metres-per-degree is unchanged;
//    local geometry, and so every baseline, is preserved exactly (output-neutral);
//  - startEpochMs moved back a whole number of weeks, the same for every game (order, gaps, time-of-day and
//    day-of-week kept, calendar date not).
// NOT blurred: the game and tournament names in baselines.json, which still name real events, and the movement
// itself (field size, orientation, timings). The pre-blur fixtures remain in git history.
internal static class TestFixtures
{
    public sealed record Baseline(
        int Game, string Name, string Tournament,
        int OnFieldSeconds, int DurationSeconds,
        double OnFieldFraction, int PointsPlayed, double FieldWidthM, double FieldLengthM,
        int LivePlaySeconds, int LivePlayDistanceM, int RestPeriods, int GameplayStartSeconds);

    public sealed record Tournament(
        int OnFieldSeconds, int DurationSeconds, double OnFieldFraction, int PointsPlayed,
        int LivePlaySeconds, int LivePlayDistanceM, int RestPeriods);

    // tryouts.json: the reference detector's numbers for each lap-pressed
    // tryout (with the laps' fit exclusion), plus the counts its laps imply.
    public sealed record TryoutBaseline(
        int Tryout, string Name, int OnFieldSeconds, int DurationSeconds, int PointsPlayed, int RestPeriods,
        int GameplayStartSeconds, double FieldWidthM, double FieldLengthM, int LivePlaySeconds,
        int LapPoints, int LapRestPeriods);

    public sealed record TryoutLap(double StartSec, double DurationSec, double AvgMps, double MaxMps);

    // Keyed by the short tag in baselines.json ("Regionals", "BigC", "Nationals").
    public sealed record Baselines(List<Baseline> Games, Dictionary<string, Tournament> Tournaments);

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

    // The same fixture as the exact PUT /api/activities/{id}/track request body,
    // for tests that go through the production decode rather than LoadTrack.
    public static string LoadTrackPayload(int game)
    {
        using var gz = new GZipStream(Resource($"game-{game:00}.json.gz"), CompressionMode.Decompress);
        using var reader = new StreamReader(gz);
        return reader.ReadToEnd();
    }

    // A lap-pressed tryout: the track plus its laps as ActivityLap rows, with
    // StartTimeGmt rebuilt from the fixture's (shifted) startEpochMs.
    public static (long StartEpochMs, List<TrackSample> Samples, List<TryoutLap> Laps, List<ActivityLap> LapRows)
        LoadTryout(int tryout)
    {
        using var gz = new GZipStream(Resource($"tryout-{tryout:00}.json.gz"), CompressionMode.Decompress);
        using var doc = JsonDocument.Parse(gz);
        var root = doc.RootElement;
        long start = root.GetProperty("startEpochMs").GetInt64();
        var s = root.GetProperty("samples");
        double[] Arr(string k) => s.GetProperty(k).EnumerateArray().Select(e => e.GetDouble()).ToArray();
        var (t, lat, lon, spd) = (Arr("t"), Arr("lat"), Arr("lon"), Arr("spd"));
        var samples = Enumerable.Range(0, t.Length)
            .Select(i => new TrackSample(t[i], lat[i], lon[i], spd[i])).ToList();
        var laps = root.GetProperty("laps").Deserialize<List<TryoutLap>>(Web)!;
        var epoch = DateTimeOffset.FromUnixTimeMilliseconds(start).UtcDateTime;
        var rows = laps.Select((l, i) => new ActivityLap
        {
            LapIndex = i + 1,
            StartTimeGmt = epoch.AddSeconds(l.StartSec),
            DurationSeconds = (decimal)l.DurationSec,
            AvgSpeedMps = (decimal)l.AvgMps,
            MaxSpeedMps = (decimal)l.MaxMps,
            IntensityType = "INTERVAL",
        }).ToList();
        return (start, samples, laps, rows);
    }

    public static List<TryoutBaseline> LoadTryoutBaselines()
    {
        using var r = Resource("tryouts.json");
        using var doc = JsonDocument.Parse(r);
        return doc.RootElement.GetProperty("tryouts").Deserialize<List<TryoutBaseline>>(Web)!;
    }

    public static Baselines LoadBaselines()
    {
        using var r = Resource("baselines.json");
        return JsonSerializer.Deserialize<Baselines>(r, Web)!;
    }
}
