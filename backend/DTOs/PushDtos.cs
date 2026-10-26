namespace Callahan.Api.DTOs;

public record PushSubscriptionKeysDto(string P256dh, string Auth);

public record CreatePushSubscriptionRequest(string Endpoint, PushSubscriptionKeysDto Keys);

// TargetWeight arrives pre-formatted ("80 kg", empty for bodyweight) so the
// watch and the Live Activity render the same string. DoneLabel is non-empty
// only when the set just finished was the last one left in the session.
// NextSetNumber/TotalSets count every row of the exercise, warmups first;
// WarmupSets says how many of those are warmups, so a surface can show
// "Warmup 1/2" then "Set 1/4" (see setPosition in the frontend's setSlot.js).
public record RestTimerScheduleRequest(
    int DurationSeconds,
    string ExerciseName,
    string TargetReps,
    int NextSetNumber,
    int TotalSets,
    bool SuppressPush = false,
    string TargetWeight = "",
    string EnteredReps = "",
    string DoneLabel = "",
    int WarmupSets = 0);

public record RestTimerScheduleResponse(string TimerId);

// Where the workout stands when no rest is running: the next set, described the
// same way a rest's is. Pushed on every set tick that does not start a rest
// (a non-last superset member), so the watch can follow along between rests.
public record RestProgressRequest(
    string ExerciseName,
    string TargetReps,
    int NextSetNumber,
    int TotalSets,
    string TargetWeight = "",
    string EnteredReps = "",
    string DoneLabel = "",
    int WarmupSets = 0);

// What GET /api/resttimer/current returns when no rest is pending but a progress
// slot is held. Deliberately has no timerId/endsAtUtc: a watch build that
// predates it parses those as missing, drops the response, and carries on.
// ProgressId changes on every push, so the watch can tell a new slot from the
// one it already shows.
public record RestProgressResponse(
    string ProgressId,
    string ExerciseName,
    string TargetReps,
    int NextSetNumber,
    int TotalSets,
    DateTimeOffset ServerNowUtc,
    string TargetWeight,
    string EnteredReps,
    string DoneLabel,
    int WarmupSets);

public record RestTimerCurrentResponse(
    string TimerId,
    DateTimeOffset EndsAtUtc,
    string ExerciseName,
    string TargetReps,
    int NextSetNumber,
    int TotalSets,
    DateTimeOffset ServerNowUtc,
    string TargetWeight,
    string EnteredReps,
    string DoneLabel,
    int WarmupSets);
