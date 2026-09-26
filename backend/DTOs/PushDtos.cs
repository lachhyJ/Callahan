namespace Callahan.Api.DTOs;

public record PushSubscriptionKeysDto(string P256dh, string Auth);

public record CreatePushSubscriptionRequest(string Endpoint, PushSubscriptionKeysDto Keys);

public record RestTimerScheduleRequest(int DurationSeconds, string ExerciseName, string TargetReps, int NextSetNumber, int TotalSets, bool SuppressPush = false);

public record RestTimerScheduleResponse(string TimerId);

public record RestTimerCurrentResponse(
    string TimerId,
    DateTimeOffset EndsAtUtc,
    string ExerciseName,
    string TargetReps,
    int NextSetNumber,
    int TotalSets,
    DateTimeOffset ServerNowUtc);
