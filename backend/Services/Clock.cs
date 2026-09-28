namespace Callahan.Api.Services;

// Every "what is today" read goes through a TimeProvider rather than
// DateTime.Now, so a test or a Playwright run can pin the date. Production
// registers TimeProvider.System, whose local zone is the container's TZ
// (Australia/Melbourne - see docker-compose.prod.yml), so behaviour there is
// unchanged. Audit stamps (CreatedAt, DeletedAt, ...) stay on DateTime.UtcNow:
// nothing needs to pin those.
public static class Clock
{
    public static DateTime LocalNow(this TimeProvider time) => time.GetLocalNow().DateTime;

    public static DateOnly Today(this TimeProvider time) => DateOnly.FromDateTime(time.LocalNow());
}

// A clock stopped at one instant, in a given zone. Registered only in
// Development when Dev:FixedNow is set (Program.cs) so the e2e visual suite
// renders the same dates on every run; also used directly by tests.
public sealed class FixedTimeProvider(DateTimeOffset now, TimeZoneInfo zone) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => now.ToUniversalTime();

    public override TimeZoneInfo LocalTimeZone => zone;

    public static FixedTimeProvider Melbourne(DateTime localNow)
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById("Australia/Melbourne");
        return new FixedTimeProvider(new DateTimeOffset(localNow, zone.GetUtcOffset(localNow)), zone);
    }
}
