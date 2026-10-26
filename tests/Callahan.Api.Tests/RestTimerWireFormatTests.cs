using System.Text.Json;
using System.Text.RegularExpressions;
using Callahan.Api.DTOs;

namespace Callahan.Api.Tests;

// The Garmin watch data field parses GET /api/resttimer/current by string key
// and fixed character offset (watch/source/RestTimerCurrentResult.mc), not by
// type. A renamed property, a naming-policy change or a different DateTimeOffset
// format would pass every other test and break the watch silently. This pins
// the JSON itself, serialised the way MVC does (web defaults: camelCase).
public class RestTimerWireFormatTests
{
    private static JsonElement Serialise()
    {
        var dto = new RestTimerCurrentResponse(
            "abc123", new DateTimeOffset(2026, 9, 24, 13, 5, 32, 420, TimeSpan.Zero),
            "Back Squat", "5", 2, 4,
            new DateTimeOffset(2026, 9, 24, 13, 4, 2, TimeSpan.Zero),
            "100 kg", "5", "", 1);
        return JsonSerializer.SerializeToElement(dto, new JsonSerializerOptions(JsonSerializerDefaults.Web));
    }

    [Fact]
    public void KeysAreExactlyWhatTheWatchReads()
    {
        var keys = Serialise().EnumerateObject().Select(p => p.Name).Order().ToList();
        Assert.Equal(
            new[] { "doneLabel", "endsAtUtc", "enteredReps", "exerciseName", "nextSetNumber", "serverNowUtc",
                    "targetReps", "targetWeight", "timerId", "totalSets", "warmupSets" }.Order(),
            keys);
    }

    // parseIso8601 reads the first 19 characters as yyyy-MM-ddTHH:mm:ss.
    [Theory]
    [InlineData("endsAtUtc", "2026-09-24T13:05:32")]
    [InlineData("serverNowUtc", "2026-09-24T13:04:02")]
    public void InstantsStartWithAFixedWidthUtcTimestamp(string key, string expectedPrefix)
    {
        var value = Serialise().GetProperty(key).GetString()!;
        Assert.Matches(new Regex(@"^\d{4}-\d\d-\d\dT\d\d:\d\d:\d\d"), value);
        Assert.StartsWith(expectedPrefix, value);
    }

    // A watch build from before progress slots parses `endsAtUtc` by key and
    // drops a response without one; if this ever gained that key (or timerId),
    // an old build would start a countdown from a progress slot.
    [Fact]
    public void ProgressResponseCarriesNoTimerKeysAndThePollersStrings()
    {
        var dto = new RestProgressResponse(
            "p1", "Calf Raise", "12", 1, 3, new DateTimeOffset(2026, 10, 27, 1, 2, 3, TimeSpan.Zero),
            "40 kg", "12", "", 0);
        var json = JsonSerializer.SerializeToElement(dto, new JsonSerializerOptions(JsonSerializerDefaults.Web));

        var keys = json.EnumerateObject().Select(p => p.Name).Order().ToList();
        Assert.Equal(
            new[] { "doneLabel", "enteredReps", "exerciseName", "nextSetNumber", "progressId", "serverNowUtc",
                    "targetReps", "targetWeight", "totalSets", "warmupSets" }.Order(),
            keys);
        Assert.StartsWith("2026-10-27T01:02:03", json.GetProperty("serverNowUtc").GetString());
        foreach (var key in new[] { "progressId", "targetWeight", "enteredReps", "doneLabel", "exerciseName", "targetReps" })
            Assert.Equal(JsonValueKind.String, json.GetProperty(key).ValueKind);
    }

    [Fact]
    public void StringSlotsAreNeverNull()
    {
        var json = Serialise();
        foreach (var key in new[] { "targetWeight", "enteredReps", "doneLabel", "exerciseName", "targetReps" })
            Assert.Equal(JsonValueKind.String, json.GetProperty(key).ValueKind);
    }
}
