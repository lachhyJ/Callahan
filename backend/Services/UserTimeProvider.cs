namespace Callahan.Api.Services;

// The app's clock. "Today" is read in a zone the user can switch at runtime
// (travel), instead of the container's fixed TZ. Singleton: the zone is loaded
// from the AppSettings table at startup and swapped by TimeZoneController.
// Past data is unaffected - session instants are UTC and day-level dates are
// stored as plain dates.
public sealed class UserTimeProvider : TimeProvider
{
    public const string SettingKey = "timezone";

    private readonly DateTimeOffset? _fixedNow;
    private volatile TimeZoneInfo _zone;

    public UserTimeProvider(TimeZoneInfo zone, DateTimeOffset? fixedNow = null)
    {
        _zone = zone;
        _fixedNow = fixedNow;
    }

    public override TimeZoneInfo LocalTimeZone => _zone;

    public override DateTimeOffset GetUtcNow() => _fixedNow?.ToUniversalTime() ?? DateTimeOffset.UtcNow;

    public void SetZone(TimeZoneInfo zone) => _zone = zone;
}
