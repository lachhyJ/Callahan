using System.Text.Json;
using Callahan.Api.Models;

namespace Callahan.Api.Services;

// Pulls Garmin's own training metrics out of a stored activity-summary blob
// (Activity.RawJson) into typed columns. Garmin computes these from HR via
// Firstbeat; they are absent on short / no-HR sessions and on manual entries,
// so every field is optional. Pure and side-effect-free apart from Apply,
// which writes the parsed values onto an Activity.
public static class GarminActivityMetrics
{
    public record Parsed(
        decimal? TrainingLoad,
        decimal? AerobicTrainingEffect,
        decimal? AnaerobicTrainingEffect,
        string? TrainingEffectLabel);

    public static readonly Parsed Empty = new(null, null, null, null);

    // Load is a whole number in Garmin's UI; the training-effect scores are a
    // 0.0-5.0 scale shown to one decimal. Round to match - the raw values
    // stay in RawJson if more precision is ever wanted.
    public static Parsed Parse(string? rawJson)
    {
        if (string.IsNullOrWhiteSpace(rawJson)) return Empty;

        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(rawJson);
        }
        catch (JsonException)
        {
            return Empty;
        }

        using (doc)
        {
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return Empty;

            return new Parsed(
                Number(root, "activityTrainingLoad") is decimal load ? Math.Round(load, 0) : null,
                Number(root, "aerobicTrainingEffect") is decimal aero ? Math.Round(aero, 1) : null,
                Number(root, "anaerobicTrainingEffect") is decimal anaero ? Math.Round(anaero, 1) : null,
                NonEmptyString(root, "trainingEffectLabel"));
        }
    }

    public static void Apply(Activity activity, string? rawJson)
    {
        var p = Parse(rawJson);
        activity.ActivityTrainingLoad = p.TrainingLoad;
        activity.AerobicTrainingEffect = p.AerobicTrainingEffect;
        activity.AnaerobicTrainingEffect = p.AnaerobicTrainingEffect;
        activity.TrainingEffectLabel = p.TrainingEffectLabel;
    }

    private static decimal? Number(JsonElement obj, string name) =>
        obj.TryGetProperty(name, out var el) && el.ValueKind == JsonValueKind.Number && el.TryGetDecimal(out var v)
            ? v
            : null;

    private static string? NonEmptyString(JsonElement obj, string name) =>
        obj.TryGetProperty(name, out var el) && el.ValueKind == JsonValueKind.String && el.GetString() is { Length: > 0 } s
            ? s
            : null;
}
