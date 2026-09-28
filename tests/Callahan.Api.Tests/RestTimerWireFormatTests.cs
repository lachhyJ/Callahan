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
            "100 kg", "5", "");
        return JsonSerializer.SerializeToElement(dto, new JsonSerializerOptions(JsonSerializerDefaults.Web));
    }

    [Fact]
    public void KeysAreExactlyWhatTheWatchReads()
    {
        var keys = Serialise().EnumerateObject().Select(p => p.Name).Order().ToList();
        Assert.Equal(
            new[] { "doneLabel", "endsAtUtc", "enteredReps", "exerciseName", "nextSetNumber", "serverNowUtc",
                    "targetReps", "targetWeight", "timerId", "totalSets" }.Order(),
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

    [Fact]
    public void StringSlotsAreNeverNull()
    {
        var json = Serialise();
        foreach (var key in new[] { "targetWeight", "enteredReps", "doneLabel", "exerciseName", "targetReps" })
            Assert.Equal(JsonValueKind.String, json.GetProperty(key).ValueKind);
    }
}
