using Callahan.Api.Services;
using Xunit;

namespace Callahan.Api.Tests;

public class UserTimeProviderTests
{
    [Fact]
    public void SwitchingZone_MovesTodayAcrossTheDateLine()
    {
        // 23:00 UTC on 4 Oct: already the 5th in Melbourne, still the 4th in Los Angeles.
        var instant = new DateTimeOffset(2026, 10, 4, 23, 0, 0, TimeSpan.Zero);
        var time = new UserTimeProvider(TimeZoneInfo.FindSystemTimeZoneById("Australia/Melbourne"), instant);
        Assert.Equal(new DateOnly(2026, 10, 5), time.Today());

        time.SetZone(TimeZoneInfo.FindSystemTimeZoneById("America/Los_Angeles"));
        Assert.Equal(new DateOnly(2026, 10, 4), time.Today());
        Assert.Equal(16, time.LocalNow().Hour);
    }
}
