import Toybox.Lang;
import Toybox.Time;

// Mirrors the backend's RestTimerCurrentResponse (backend/DTOs/PushDtos.cs).
// ASP.NET Core's default System.Text.Json policy is camelCase, matching the
// existing frontend's request bodies (see frontend/src/api/client.js) — so
// the wire keys are camelCase, not the C# PascalCase property names. The
// response also carries nextSetNumber/totalSets: the set coming up after the
// rest (1-based) and its exercise's set count, both counting warmups (which
// come first); warmupSets says how many of those rows are warmups.
//
// PORTS to a full watch app unchanged.
class RestTimerCurrentResult {
    var timerId as String;
    var endsAtUtc as Moment;
    var exerciseName as String;
    var targetReps as String;
    // The server's clock at the moment it answered — lets RestTimerState
    // correct for skew between the watch's clock and the server's. Garmin
    // devices sync time via GPS/phone so skew is normally small, but it
    // isn't zero.
    var serverNowUtc as Moment;
    // The set coming up after this rest: pre-formatted weight ("80 kg", or
    // empty for bodyweight), the reps typed into its row (may be empty), and
    // a non-empty doneLabel ("Finisher?" / "Finished") when there's no next
    // set at all.
    var targetWeight as String;
    var enteredReps as String;
    var doneLabel as String;
    var nextSetNumber as Number;
    var totalSets as Number;
    var warmupSets as Number;

    function initialize(
        timerIdIn as String,
        endsAtUtcIn as Moment,
        exerciseNameIn as String,
        targetRepsIn as String,
        serverNowUtcIn as Moment,
        targetWeightIn as String,
        enteredRepsIn as String,
        doneLabelIn as String,
        nextSetNumberIn as Number,
        totalSetsIn as Number,
        warmupSetsIn as Number
    ) {
        timerId = timerIdIn;
        endsAtUtc = endsAtUtcIn;
        exerciseName = exerciseNameIn;
        targetReps = targetRepsIn;
        serverNowUtc = serverNowUtcIn;
        targetWeight = targetWeightIn;
        enteredReps = enteredRepsIn;
        doneLabel = doneLabelIn;
        nextSetNumber = nextSetNumberIn;
        totalSets = totalSetsIn;
        warmupSets = warmupSetsIn;
    }

    // The response's endsAtUtc is ISO-8601, e.g. "2026-09-24T13:05:32.420582+00:00".
    // Moment has no ISO-8601 parser and Monkey C's String has no split(), so
    // this picks the date/time fields apart by fixed offset. .NET's default
    // DateTimeOffset serialization is always
    // "yyyy-MM-ddTHH:mm:ss.fffffff+00:00" (round-trip "o" format) — the
    // first 19 characters are fixed-width regardless of the fractional
    // digits or trailing offset, which this drops (irrelevant at a 1Hz
    // countdown granularity, and the offset is always +00:00 since the
    // backend only ever emits UtcNow-derived instants).
    // Returns null for anything shorter than the fixed-width prefix or with
    // a non-numeric field, so a malformed response is dropped rather than
    // crashing the data field.
    static function parseIso8601(iso as String) as Moment? {
        if (iso.length() < 19) {
            return null;
        }
        var year = field(iso, 0, 4);
        var month = field(iso, 5, 7);
        var day = field(iso, 8, 10);
        var hour = field(iso, 11, 13);
        var minute = field(iso, 14, 16);
        var second = field(iso, 17, 19);
        if (year == null || month == null || day == null
                || hour == null || minute == null || second == null) {
            return null;
        }

        return Time.Gregorian.moment({
            :year => year,
            :month => month,
            :day => day,
            :hour => hour,
            :minute => minute,
            :second => second
        });
    }

    private static function field(iso as String, from as Number, to as Number) as Number? {
        var part = iso.substring(from, to);
        return part != null ? part.toNumber() : null;
    }

    // Null when either timestamp doesn't parse; the caller treats that like
    // any other response with no usable result.
    static function fromDictionary(data as Dictionary<String, Object?>) as RestTimerCurrentResult? {
        var endsAtUtc = parseIso8601(stringOrEmpty(data["endsAtUtc"]));
        var serverNowUtc = parseIso8601(stringOrEmpty(data["serverNowUtc"]));
        if (endsAtUtc == null || serverNowUtc == null) {
            return null;
        }
        return new RestTimerCurrentResult(
            data["timerId"] as String,
            endsAtUtc,
            data["exerciseName"] as String,
            data["targetReps"] as String,
            serverNowUtc,
            stringOrEmpty(data["targetWeight"]),
            stringOrEmpty(data["enteredReps"]),
            stringOrEmpty(data["doneLabel"]),
            numberOrZero(data["nextSetNumber"]),
            numberOrZero(data["totalSets"]),
            numberOrZero(data["warmupSets"])
        );
    }

    private static function numberOrZero(value as Object?) as Number {
        return value instanceof Number ? value : 0;
    }

    // Tolerates a backend deployed before these fields existed.
    private static function stringOrEmpty(value as Object?) as String {
        return value instanceof String ? value : "";
    }
}
