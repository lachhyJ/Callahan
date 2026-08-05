namespace Callahan.Api.DTOs;

public record PushSubscriptionKeysDto(string P256dh, string Auth);

public record CreatePushSubscriptionRequest(string Endpoint, PushSubscriptionKeysDto Keys);

public record RestTimerScheduleRequest(int DurationSeconds, string ExerciseName);

public record RestTimerScheduleResponse(string TimerId);
