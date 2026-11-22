using System.Text.Json;

namespace Callahan.Api.Services;

// Per-session figures for a Club Training recording that Garmin doesn't hand
// over as columns: high-speed distance and sprint count from the stored GPS
// track, max HR and time-in-zone from the stored RawJson.
//
// Club sessions are scrimmages recorded as one continuous activity, so the
// lap-based FieldGeometry does not apply; distance stays the whole-recording
// figure (Activity.DistanceKm). The speed threshold is provisional: the
// high-speed-effort work is parked on picking it from real play, and metres
// above a threshold swing about 2x between 3.5 and 5 m/s, so the page shows
// these as comparisons across sessions, never as absolutes. Top speed is
// deliberately not reported - smart recording samples every 1-10 s and the
// stream has single-sample spikes.
public static class ClubSessionMetrics
{
    public const double HighSpeedMps = 5.5;
    // Efforts closer together than this are one effort (a stutter at the
    // threshold, or one dropped sample, is not two sprints).
    public const double EffortGapSeconds = 10;
    // Garmin's smart recording leaves gaps of up to ~10 s; integrating speed
    // across a longer gap would invent distance, so those intervals are skipped.
    public const double MaxIntegrableGapSeconds = 10;

    public sealed record TrackFigures(double HighSpeedDistanceM, int SprintCount);

    public static TrackFigures FromTrack(IReadOnlyList<TrackSample> samples)
    {
        double metres = 0;
        int efforts = 0;
        double lastAbove = double.NegativeInfinity;
        for (int i = 0; i < samples.Count; i++)
        {
            var s = samples[i];
            if (s.Spd < HighSpeedMps) continue;
            if (s.T - lastAbove > EffortGapSeconds) efforts++;
            lastAbove = s.T;
            if (i + 1 < samples.Count)
            {
                var dt = samples[i + 1].T - s.T;
                if (dt > 0 && dt <= MaxIntegrableGapSeconds) metres += s.Spd * dt;
            }
        }
        return new TrackFigures(Math.Round(metres, 0), efforts);
    }

    public sealed record RawExtras(int? MaxHeartRate, IReadOnlyList<int>? ZoneSeconds);

    // Zones 1-5 in seconds, or null unless all five are present (a partial set
    // would draw a misleading bar).
    public static RawExtras ParseRaw(string? rawJson)
    {
        if (string.IsNullOrWhiteSpace(rawJson)) return new RawExtras(null, null);
        try
        {
            using var doc = JsonDocument.Parse(rawJson);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return new RawExtras(null, null);
            int? max = root.TryGetProperty("maxHR", out var m) && m.ValueKind == JsonValueKind.Number
                ? (int)Math.Round(m.GetDouble()) : null;
            var zones = new List<int>(5);
            for (int z = 1; z <= 5; z++)
            {
                if (root.TryGetProperty($"hrTimeInZone_{z}", out var v) && v.ValueKind == JsonValueKind.Number)
                    zones.Add((int)Math.Round(v.GetDouble()));
                else return new RawExtras(max, null);
            }
            return new RawExtras(max, zones);
        }
        catch (JsonException)
        {
            return new RawExtras(null, null);
        }
    }
}
