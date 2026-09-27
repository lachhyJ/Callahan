namespace Callahan.Api.DTOs;

public record PushSubscriptionKeysDto(string P256dh, string Auth);

public record CreatePushSubscriptionRequest(string Endpoint, PushSubscriptionKeysDto Keys);

// TargetWeight arrives pre-formatted ("80 kg", empty for bodyweight) so the
// watch and the Live Activity render the same string. DoneLabel is non-empty
// only when the set just finished was the last one left in the session.
public record RestTimerScheduleRequest(
    int DurationSeconds,
    string ExerciseName,
    string TargetReps,
    int NextSetNumber,
    int TotalSets,
    bool SuppressPush = false,
    string TargetWeight = "",
    string EnteredReps = "",
    string DoneLabel = "");

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
    string DoneLabel);
