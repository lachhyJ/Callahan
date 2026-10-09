using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace Callahan.Api.Services.Calendar;

// Reads the Training calendar over CalDAV, the way Moxie's icloud-cal.py reads
// iCloud: sign in with an Apple ID and an app-specific password, find the calendar
// by name, ask for the week's events. Find-by-name rather than a configured URL
// because iCloud's calendar addresses are per-account and not meant to be typed.
//
// Config: Calendar:Username, Calendar:Password (an app-specific password, never the
// Apple ID password), Calendar:CalendarName (default "Training") and
// Calendar:ServerUrl (default iCloud). Without a username and password it is off.
public class CalDavCalendarClient(
    HttpClient http, IConfiguration config, TimeProvider time, ILogger<CalDavCalendarClient> logger) : ICalendarClient
{
    private static readonly XNamespace D = "DAV:";
    private static readonly XNamespace C = "urn:ietf:params:xml:ns:caldav";

    // The calendar's address is stable, so it is found once per process.
    private static readonly ConcurrentDictionary<string, Uri> CalendarUrls = new();

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(config["Calendar:Username"]) && !string.IsNullOrWhiteSpace(config["Calendar:Password"]);

    public async Task<IReadOnlyList<CalendarEvent>> GetEventsAsync(DateOnly from, DateOnly to, CancellationToken ct)
    {
        if (!IsConfigured) throw new CalendarUnavailableException("The calendar isn't set up on this server.");

        try
        {
            var calendar = await CalendarUrlAsync(ct);

            // Padded a day either side: a floating time has no zone, so the server
            // compares it as UTC and an event near midnight can fall just outside.
            // The caller filters to the exact training days.
            var range = $"""<c:time-range start="{Utc(from.AddDays(-1))}" end="{Utc(to.AddDays(2))}"/>""";
            var body = $"""
                <c:calendar-query xmlns:d="DAV:" xmlns:c="urn:ietf:params:xml:ns:caldav">
                  <d:prop><d:getetag/><c:calendar-data/></d:prop>
                  <c:filter><c:comp-filter name="VCALENDAR"><c:comp-filter name="VEVENT">{range}</c:comp-filter></c:comp-filter></c:filter>
                </c:calendar-query>
                """;

            var doc = await SendAsync(new HttpMethod("REPORT"), calendar, "1", body, ct);
            var events = new List<CalendarEvent>();
            foreach (var response in doc.Descendants(D + "response"))
            {
                var ics = response.Descendants(C + "calendar-data").FirstOrDefault()?.Value;
                if (string.IsNullOrWhiteSpace(ics)) continue;
                var etag = response.Descendants(D + "getetag").FirstOrDefault()?.Value;
                var href = response.Element(D + "href")?.Value;
                events.AddRange(IcalEventParser.Parse(ics, time.LocalTimeZone, etag, href));
            }
            return events;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or XmlException)
        {
            logger.LogWarning(ex, "Couldn't read the Training calendar");
            throw new CalendarUnavailableException("Couldn't reach your calendar.", ex);
        }
    }

    public async Task CreateEventAsync(string title, DateOnly day, TimeOfDay part, CancellationToken ct)
    {
        EnsureConfigured();
        var uid = IcalEventWriter.NewUid();
        var ics = IcalEventWriter.Create(uid, title, TimeOfDayBuckets.DefaultStart(day, part), time.GetUtcNow().UtcDateTime);
        // The file name leaves out the "@callahan" half of the UID: an "@" in a URL path is
        // one more thing a server could handle oddly, and the name carries no meaning.
        await WriteAsync(HttpMethod.Put, uid[..uid.IndexOf('@')] + ".ics", null, ics, ifMatch: null, createOnly: true, ct);
    }

    public async Task MoveEventAsync(CalendarEvent existing, DateOnly day, TimeOfDay part, CancellationToken ct)
    {
        EnsureConfigured();
        if (existing.Href is null || existing.Ics is null)
        {
            throw new CalendarUnavailableException("That event can't be moved because its address is unknown.");
        }

        string ics;
        try
        {
            ics = IcalEventWriter.Reschedule(existing.Ics, TimeOfDayBuckets.DefaultStart(day, part), time.GetUtcNow().UtcDateTime);
        }
        catch (FormatException ex)
        {
            throw new CalendarUnavailableException(ex.Message, ex);
        }
        await WriteAsync(HttpMethod.Put, null, existing.Href, ics, existing.ETag, createOnly: false, ct);
    }

    public async Task DeleteEventAsync(CalendarEvent existing, CancellationToken ct)
    {
        EnsureConfigured();
        if (existing.Href is null) throw new CalendarUnavailableException("That event can't be removed because its address is unknown.");
        await WriteAsync(HttpMethod.Delete, null, existing.Href, null, existing.ETag, createOnly: false, ct);
    }

    private void EnsureConfigured()
    {
        if (!IsConfigured) throw new CalendarUnavailableException("The calendar isn't set up on this server.");
    }

    // One write to the Training calendar. A new event goes to <calendar>/<name>, an
    // existing one to the address it was read from.
    private async Task WriteAsync(HttpMethod method, string? newName, string? href, string? body, string? ifMatch, bool createOnly, CancellationToken ct)
    {
        try
        {
            var calendar = await CalendarUrlAsync(ct);
            var url = href is null ? new Uri(calendar, newName!) : new Uri(calendar, href);

            using var request = new HttpRequestMessage(method, url);
            var credentials = Convert.ToBase64String(
                Encoding.UTF8.GetBytes($"{config["Calendar:Username"]}:{config["Calendar:Password"]}"));
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);
            if (ifMatch is not null) request.Headers.TryAddWithoutValidation("If-Match", ifMatch);
            if (createOnly) request.Headers.TryAddWithoutValidation("If-None-Match", "*");
            if (body is not null) request.Content = new StringContent(body, Encoding.UTF8, "text/calendar");

            using var response = await http.SendAsync(request, ct);

            if (response.IsSuccessStatusCode) return;
            // Removing something already gone is the outcome that was asked for.
            if (method == HttpMethod.Delete && response.StatusCode == HttpStatusCode.NotFound) return;
            if (response.StatusCode == HttpStatusCode.PreconditionFailed) throw new CalendarConflictException();
            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            {
                throw new CalendarUnavailableException("The calendar rejected the login. Check the app-specific password.");
            }
            throw new CalendarUnavailableException($"The calendar server answered {(int)response.StatusCode}.");
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            logger.LogWarning(ex, "Couldn't write to the Training calendar");
            throw new CalendarUnavailableException("Couldn't reach your calendar.", ex);
        }
    }

    private static string Utc(DateOnly d) => d.ToDateTime(TimeOnly.MinValue).ToString("yyyyMMdd'T'HHmmss'Z'");

    private async Task<Uri> CalendarUrlAsync(CancellationToken ct)
    {
        var root = new Uri(config["Calendar:ServerUrl"] is { Length: > 0 } s ? s : "https://caldav.icloud.com/");
        var name = config["Calendar:CalendarName"] is { Length: > 0 } n ? n : "Training";
        var key = $"{root}|{config["Calendar:Username"]}|{name}";
        if (CalendarUrls.TryGetValue(key, out var cached)) return cached;

        var principal = await HrefAsync(root, "<d:current-user-principal/>", D + "current-user-principal", ct);
        var home = await HrefAsync(principal, "<c:calendar-home-set/>", C + "calendar-home-set", ct);

        var list = await SendAsync(new HttpMethod("PROPFIND"), home, "1",
            PropFind("<d:displayname/><d:resourcetype/>"), ct);

        foreach (var response in list.Descendants(D + "response"))
        {
            var display = response.Descendants(D + "displayname").FirstOrDefault()?.Value;
            var isCalendar = response.Descendants(D + "resourcetype").Descendants(C + "calendar").Any();
            if (!isCalendar || !string.Equals(display, name, StringComparison.OrdinalIgnoreCase)) continue;
            var href = response.Element(D + "href")?.Value;
            if (href is null) continue;
            return CalendarUrls[key] = new Uri(home, href);
        }

        throw new CalendarUnavailableException($"There's no calendar called \"{name}\" on this account.");
    }

    private async Task<Uri> HrefAsync(Uri at, string prop, XName container, CancellationToken ct)
    {
        var doc = await SendAsync(new HttpMethod("PROPFIND"), at, "0", PropFind(prop), ct);
        var href = doc.Descendants(container).Descendants(D + "href").FirstOrDefault()?.Value;
        if (string.IsNullOrWhiteSpace(href)) throw new CalendarUnavailableException("The calendar server didn't say where your calendars are.");
        return new Uri(at, href);
    }

    private static string PropFind(string props) =>
        $"""<d:propfind xmlns:d="DAV:" xmlns:c="urn:ietf:params:xml:ns:caldav"><d:prop>{props}</d:prop></d:propfind>""";

    private async Task<XDocument> SendAsync(HttpMethod method, Uri url, string depth, string body, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(method, url);
        var credentials = Convert.ToBase64String(
            Encoding.UTF8.GetBytes($"{config["Calendar:Username"]}:{config["Calendar:Password"]}"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);
        request.Headers.Add("Depth", depth);
        request.Content = new StringContent(body, Encoding.UTF8, "application/xml");

        using var response = await http.SendAsync(request, ct);

        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            throw new CalendarUnavailableException("The calendar rejected the login. Check the app-specific password.");
        }
        if (response.StatusCode != HttpStatusCode.MultiStatus)
        {
            throw new CalendarUnavailableException($"The calendar server answered {(int)response.StatusCode}.");
        }

        return XDocument.Parse(await response.Content.ReadAsStringAsync(ct));
    }
}
