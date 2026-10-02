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
