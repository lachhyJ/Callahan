using System.Net;
using System.Text;
using Callahan.Api.Services;
using Callahan.Api.Services.Calendar;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace Callahan.Api.Tests;

// The CalDAV client against canned iCloud-shaped responses: discovery (principal,
// calendar home, the calendar by name), the week's REPORT, and the ways it fails.
// Whether a real iCloud answers like this is the separate spike, not these tests.
public class CalDavCalendarClientTests
{
    private const string Dav = "xmlns:d=\"DAV:\" xmlns:c=\"urn:ietf:params:xml:ns:caldav\"";

    private record Seen(string Method, string Path, string? Depth, string? Auth, string Body, string? IfMatch = null, string? IfNoneMatch = null);

    private sealed class Stub(Func<Seen, (HttpStatusCode, string)> reply) : HttpMessageHandler
    {
        public List<Seen> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var seen = new Seen(
                request.Method.Method,
                request.RequestUri!.AbsolutePath,
                request.Headers.TryGetValues("Depth", out var d) ? d.Single() : null,
                request.Headers.Authorization?.ToString(),
                request.Content is null ? "" : await request.Content.ReadAsStringAsync(ct),
                request.Headers.TryGetValues("If-Match", out var m) ? m.Single() : null,
                request.Headers.TryGetValues("If-None-Match", out var n) ? n.Single() : null);
            Requests.Add(seen);
            var (status, body) = reply(seen);
            return new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/xml") };
        }
    }

    private static string Multi(string responses) => $"<d:multistatus {Dav}>{responses}</d:multistatus>";

    private static (HttpStatusCode, string) ICloudLike(Seen r, string calendarName = "Training")
    {
        if (r.Method == "REPORT")
        {
            return (HttpStatusCode.MultiStatus, Multi($"""
                <d:response><d:href>/123/calendars/training/a.ics</d:href><d:propstat><d:prop>
                  <d:getetag>"e1"</d:getetag>
                  <c:calendar-data>BEGIN:VCALENDAR
                BEGIN:VEVENT
                UID:a
                SUMMARY:Gym 1
                DTSTART:20261006T070000
                END:VEVENT
                END:VCALENDAR</c:calendar-data></d:prop></d:propstat></d:response>
                """));
        }
        return r.Path switch
        {
            "/" => (HttpStatusCode.MultiStatus, Multi("<d:response><d:href>/</d:href><d:propstat><d:prop><d:current-user-principal><d:href>/123/principal/</d:href></d:current-user-principal></d:prop></d:propstat></d:response>")),
            "/123/principal/" => (HttpStatusCode.MultiStatus, Multi("<d:response><d:href>/123/principal/</d:href><d:propstat><d:prop><c:calendar-home-set><d:href>https://p01-caldav.icloud.com:443/123/calendars/</d:href></c:calendar-home-set></d:prop></d:propstat></d:response>")),
            "/123/calendars/" => (HttpStatusCode.MultiStatus, Multi($"""
                <d:response><d:href>/123/calendars/home/</d:href><d:propstat><d:prop><d:displayname>Home</d:displayname><d:resourcetype><d:collection/><c:calendar/></d:resourcetype></d:prop></d:propstat></d:response>
                <d:response><d:href>/123/calendars/training/</d:href><d:propstat><d:prop><d:displayname>{calendarName}</d:displayname><d:resourcetype><d:collection/><c:calendar/></d:resourcetype></d:prop></d:propstat></d:response>
                <d:response><d:href>/123/calendars/</d:href><d:propstat><d:prop><d:displayname>Calendars</d:displayname><d:resourcetype><d:collection/></d:resourcetype></d:prop></d:propstat></d:response>
                """)),
            _ => (HttpStatusCode.NotFound, "")
        };
    }

    // A unique username per test: the calendar's address is cached per process.
    private static CalDavCalendarClient Client(Stub stub, string user, string? password = "app-pw", string? name = null)
    {
        var settings = new Dictionary<string, string?> { ["Calendar:Username"] = user, ["Calendar:Password"] = password };
        if (name is not null) settings["Calendar:CalendarName"] = name;
        var config = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        return new CalDavCalendarClient(
            new HttpClient(stub), config, FixedTimeProvider.Melbourne(new DateTime(2026, 10, 6, 9, 0, 0)),
            NullLogger<CalDavCalendarClient>.Instance);
    }

    private static readonly DateOnly Mon = new(2026, 10, 5);

    [Fact]
    public async Task FindsTheCalendarByNameAndReadsTheWeeksEvents()
    {
        var stub = new Stub(r => ICloudLike(r));
        var events = await Client(stub, "find@example.com").GetEventsAsync(Mon, Mon.AddDays(6), default);

        var e = Assert.Single(events);
        Assert.Equal(("a", "Gym 1", new DateOnly(2026, 10, 6), (TimeOfDay?)TimeOfDay.Morning, "\"e1\""), (e.Uid, e.Title, e.Day, e.Part, e.ETag));
        Assert.Equal(["PROPFIND", "PROPFIND", "PROPFIND", "REPORT"], stub.Requests.Select(r => r.Method));
        Assert.Equal("/123/calendars/training/", stub.Requests[^1].Path); // the REPORT went to the Training calendar
    }

    [Fact]
    public async Task SignsInWithBasicAuthOnEveryRequestAndPadsTheTimeRange()
    {
        var stub = new Stub(r => ICloudLike(r));
        await Client(stub, "auth@example.com", "secret-pw").GetEventsAsync(Mon, Mon.AddDays(6), default);

        var expected = "Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes("auth@example.com:secret-pw"));
        Assert.All(stub.Requests, r => Assert.Equal(expected, r.Auth));
        // A floating time is compared as UTC by the server, so the window is a day wider each side.
        var report = stub.Requests[^1].Body;
        Assert.Contains("start=\"20261004T000000Z\"", report);
        Assert.Contains("end=\"20261013T000000Z\"", report);
        Assert.Equal("1", stub.Requests[^1].Depth);
    }

    [Fact]
    public async Task TheCalendarAddressIsRememberedSoTheNextReadIsOneRequest()
    {
        var stub = new Stub(r => ICloudLike(r));
        var client = Client(stub, "cache@example.com");
        await client.GetEventsAsync(Mon, Mon.AddDays(6), default);
        stub.Requests.Clear();

        await client.GetEventsAsync(Mon, Mon.AddDays(6), default);

        Assert.Equal(["REPORT"], stub.Requests.Select(r => r.Method));
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    public async Task ARejectedLoginSaysToCheckThePassword(HttpStatusCode status)
    {
        var stub = new Stub(_ => (status, ""));
        var ex = await Assert.ThrowsAsync<CalendarUnavailableException>(
            () => Client(stub, $"reject{(int)status}@example.com").GetEventsAsync(Mon, Mon.AddDays(6), default));
        Assert.Contains("app-specific password", ex.Message);
    }

    [Fact]
    public async Task AMissingCalendarNamesTheOneItLookedFor()
    {
        var stub = new Stub(r => ICloudLike(r, calendarName: "Something else"));
        var ex = await Assert.ThrowsAsync<CalendarUnavailableException>(
            () => Client(stub, "missing@example.com").GetEventsAsync(Mon, Mon.AddDays(6), default));
        Assert.Contains("\"Training\"", ex.Message);
    }

    [Fact]
    public async Task TheCalendarNameIsConfigurableAndCaseInsensitive()
    {
        var stub = new Stub(r => ICloudLike(r, calendarName: "My Training"));
        var events = await Client(stub, "named@example.com", name: "my training").GetEventsAsync(Mon, Mon.AddDays(6), default);
        Assert.Single(events);
    }

    [Fact]
    public async Task ANetworkFailureBecomesCalendarUnavailable()
    {
        var client = Client(new Stub(_ => throw new HttpRequestException("no route")), "net@example.com");
        await Assert.ThrowsAsync<CalendarUnavailableException>(() => client.GetEventsAsync(Mon, Mon.AddDays(6), default));
    }

    [Theory]
    [InlineData(null, "pw", false)]
    [InlineData("a@b.c", null, false)]
    [InlineData("", "pw", false)]
    [InlineData("a@b.c", "pw", true)]
    public void ItIsOnlyConfiguredWithBothAUsernameAndAPassword(string? user, string? pw, bool expected)
    {
        Assert.Equal(expected, Client(new Stub(_ => (HttpStatusCode.OK, "")), user!, pw).IsConfigured);
    }

    [Fact]
    public async Task WithNoCredentialsItDoesNotCallOut()
    {
        var stub = new Stub(_ => (HttpStatusCode.OK, ""));
        await Assert.ThrowsAsync<CalendarUnavailableException>(
            () => Client(stub, "", null).GetEventsAsync(Mon, Mon.AddDays(6), default));
        Assert.Empty(stub.Requests);
    }

    // --- writes ------------------------------------------------------------------

    private static readonly CalendarEvent Existing = new(
        "a", "Gym 1", new DateOnly(2026, 10, 6), TimeOfDay.Morning, "\"e1\"", "/123/calendars/training/a.ics",
        "BEGIN:VCALENDAR\r\nBEGIN:VEVENT\r\nUID:a\r\nSUMMARY:Gym 1\r\nDESCRIPTION:Heavy\r\nDTSTART:20261006T070000\r\nDTEND:20261006T083000\r\nEND:VEVENT\r\nEND:VCALENDAR\r\n");

    private static Stub WritesReturn(HttpStatusCode status) =>
        new(r => r.Method is "PUT" or "DELETE" ? (status, "") : ICloudLike(r));

    [Fact]
    public async Task ANewEventIsPutInTheTrainingCalendarAndOnlyIfItDoesNotExist()
    {
        var stub = WritesReturn(HttpStatusCode.Created);

        await Client(stub, "create@example.com").CreateEventAsync("Gym 2", new DateOnly(2026, 10, 8), TimeOfDay.Evening, default);

        var put = stub.Requests.Single(r => r.Method == "PUT");
        Assert.StartsWith("/123/calendars/training/callahan-", put.Path);
        Assert.EndsWith(".ics", put.Path);
        Assert.DoesNotContain("@", put.Path);
        Assert.Matches(@"^callahan-[0-9a-f]{32}$", put.Body.Split("\r\n").Single(l => l.StartsWith("UID:"))[4..].Replace("@callahan", ""));
        Assert.Equal("*", put.IfNoneMatch); // never overwrites an event that is already there
        Assert.Null(put.IfMatch);
        Assert.Contains("SUMMARY:Gym 2", put.Body);
        Assert.Contains("DTSTART:20261008T180000", put.Body);
        Assert.Contains("DTEND:20261008T193000", put.Body);
    }

    [Fact]
    public async Task AMoveRewritesOnlyTheTimeAndCarriesTheETagItWasReadWith()
    {
        var stub = WritesReturn(HttpStatusCode.NoContent);

        await Client(stub, "move@example.com").MoveEventAsync(Existing, new DateOnly(2026, 10, 8), TimeOfDay.Arvo, default);

        var put = stub.Requests.Single(r => r.Method == "PUT");
        Assert.Equal("/123/calendars/training/a.ics", put.Path);
        Assert.Equal("\"e1\"", put.IfMatch);
        Assert.Null(put.IfNoneMatch);
        Assert.Contains("DESCRIPTION:Heavy", put.Body);
        Assert.Contains("DTSTART:20261008T120000", put.Body);
        Assert.Contains("DTEND:20261008T133000", put.Body); // still 90 minutes, as it was
    }

    [Fact]
    public async Task ADeleteCarriesTheETagToo()
    {
        var stub = WritesReturn(HttpStatusCode.NoContent);

        await Client(stub, "delete@example.com").DeleteEventAsync(Existing, default);

        var delete = stub.Requests.Single(r => r.Method == "DELETE");
        Assert.Equal(("/123/calendars/training/a.ics", "\"e1\""), (delete.Path, delete.IfMatch));
    }

    [Fact]
    public async Task DeletingSomethingAlreadyGoneIsNotAnError()
    {
        await Client(WritesReturn(HttpStatusCode.NotFound), "gone@example.com").DeleteEventAsync(Existing, default);
    }

    [Fact]
    public async Task AnEventChangedSinceItWasReadIsAConflictForMovesAndDeletes()
    {
        var client = Client(WritesReturn(HttpStatusCode.PreconditionFailed), "conflict@example.com");

        await Assert.ThrowsAsync<CalendarConflictException>(
            () => client.MoveEventAsync(Existing, new DateOnly(2026, 10, 8), TimeOfDay.Arvo, default));
        await Assert.ThrowsAsync<CalendarConflictException>(() => client.DeleteEventAsync(Existing, default));
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, "app-specific password")]
    [InlineData(HttpStatusCode.InternalServerError, "500")]
    public async Task OtherWriteFailuresAreCalendarUnavailable(HttpStatusCode status, string saying)
    {
        var client = Client(WritesReturn(status), $"fail{(int)status}@example.com");
        var ex = await Assert.ThrowsAsync<CalendarUnavailableException>(
            () => client.CreateEventAsync("Gym 2", new DateOnly(2026, 10, 8), TimeOfDay.Evening, default));
        Assert.Contains(saying, ex.Message);
    }

    [Fact]
    public async Task AnEventWithNoAddressOrBodyCannotBeMovedOrRemoved()
    {
        var stub = WritesReturn(HttpStatusCode.NoContent);
        var client = Client(stub, "noaddr@example.com");
        var bare = Existing with { Href = null, Ics = null };

        await Assert.ThrowsAsync<CalendarUnavailableException>(
            () => client.MoveEventAsync(bare, new DateOnly(2026, 10, 8), TimeOfDay.Arvo, default));
        await Assert.ThrowsAsync<CalendarUnavailableException>(() => client.DeleteEventAsync(bare, default));
        Assert.DoesNotContain(stub.Requests, r => r.Method is "PUT" or "DELETE");
    }

    [Fact]
    public async Task WritesNeedCredentialsAndDoNotCallOutWithout()
    {
        var stub = WritesReturn(HttpStatusCode.Created);
        await Assert.ThrowsAsync<CalendarUnavailableException>(
            () => Client(stub, "", null).CreateEventAsync("Gym 2", new DateOnly(2026, 10, 8), TimeOfDay.Evening, default));
        Assert.Empty(stub.Requests);
    }
}
