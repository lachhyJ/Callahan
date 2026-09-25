import Toybox.Lang;
import Toybox.Time;

// Mirrors the backend's RestTimerCurrentResponse (backend/DTOs/PushDtos.cs).
// ASP.NET Core's default System.Text.Json policy is camelCase, matching the
// existing frontend's request bodies (see frontend/src/api/client.js) — so
// the wire keys are timerId/endsAtUtc/exerciseName/targetReps/nextSetNumber/
// totalSets, not the C# PascalCase property names.
//
// PORTS to a full watch app unchanged.
class RestTimerCurrentResult {
    var timerId as String;
    var endsAtUtc as Moment;
    var exerciseName as String;
    var targetReps as String;
    var nextSetNumber as Number;
    var totalSets as Number;
    // The server's clock at the moment it answered — lets RestTimerState
    // correct for skew between the watch's clock and the server's (plan
    // 2.4). Garmin devices sync time via GPS/phone so skew is normally
    // small, but it isn't zero.
    var serverNowUtc as Moment;

    function initialize(
        timerIdIn as String,
        endsAtUtcIn as Moment,
        exerciseNameIn as String,
        targetRepsIn as String,
        nextSetNumberIn as Number,
        totalSetsIn as Number,
        serverNowUtcIn as Moment
    ) {
        timerId = timerIdIn;
        endsAtUtc = endsAtUtcIn;
        exerciseName = exerciseNameIn;
        targetReps = targetRepsIn;
        nextSetNumber = nextSetNumberIn;
        totalSets = totalSetsIn;
        serverNowUtc = serverNowUtcIn;
    }

    // The response's endsAtUtc is ISO-8601, e.g. "2026-09-24T13:05:32.1234567Z".
    // Moment has no ISO-8601 parser and Monkey C's String has no split(), so
    // this picks the date/time fields apart by fixed offset. .NET's default
    // DateTimeOffset serialization is always
    // "yyyy-MM-ddTHH:mm:ss.fffffff+00:00" (round-trip "o" format) — the
    // first 19 characters are fixed-width regardless of the fractional
    // digits or trailing offset, which this drops (irrelevant at a 1Hz
    // countdown granularity, and the offset is always +00:00 since the
    // backend only ever emits UtcNow-derived instants).
    static function parseIso8601(iso as String) as Moment {
        var year = iso.substring(0, 4).toNumber();
        var month = iso.substring(5, 7).toNumber();
        var day = iso.substring(8, 10).toNumber();
        var hour = iso.substring(11, 13).toNumber();
        var minute = iso.substring(14, 16).toNumber();
        var second = iso.substring(17, 19).toNumber();

        return Time.Gregorian.moment({
            :year => year,
            :month => month,
            :day => day,
            :hour => hour,
            :minute => minute,
            :second => second
        });
    }

    static function fromDictionary(data as Dictionary) as RestTimerCurrentResult {
        return new RestTimerCurrentResult(
            data["timerId"] as String,
            parseIso8601(data["endsAtUtc"] as String),
            data["exerciseName"] as String,
            data["targetReps"] as String,
            data["nextSetNumber"] as Number,
            data["totalSets"] as Number,
            parseIso8601(data["serverNowUtc"] as String)
        );
    }
}
