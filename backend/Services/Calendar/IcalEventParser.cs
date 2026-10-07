using System.Globalization;
using System.Text;

namespace Callahan.Api.Services.Calendar;

// A deliberately small iCalendar reader: just the VEVENT fields the planner uses.
// Recurring events (RRULE) and cancelled ones are skipped - the Training calendar
// holds one-off sessions, and a recurring master would only show on its first
// date, which is worse than not showing.
public static class IcalEventParser
{
    public static IReadOnlyList<CalendarEvent> Parse(string ics, TimeZoneInfo userZone, string? etag = null, string? href = null)
    {
        var events = new List<CalendarEvent>();
        Dictionary<string, (string Params, string Value)>? current = null;

        foreach (var (name, parameters, value) in Lines(ics))
        {
            if (name == "BEGIN" && value == "VEVENT") { current = new(); continue; }
            if (name == "END" && value == "VEVENT")
            {
                if (current is not null && Build(current, userZone, etag, href) is { } ev) events.Add(ev);
                current = null;
                continue;
            }
            // First occurrence wins, which keeps a VALARM's own properties from
            // overwriting the event's (they are nested inside it).
            if (current is not null) current.TryAdd(name, (parameters, value));
        }

        return events;
    }

    private static CalendarEvent? Build(Dictionary<string, (string Params, string Value)> p, TimeZoneInfo zone, string? etag, string? href)
    {
        if (!p.TryGetValue("UID", out var uid) || string.IsNullOrWhiteSpace(uid.Value)) return null;
        if (p.ContainsKey("RRULE")) return null;
        if (p.TryGetValue("STATUS", out var status) && status.Value.Equals("CANCELLED", StringComparison.OrdinalIgnoreCase)) return null;
        if (!p.TryGetValue("DTSTART", out var start)) return null;

        var title = p.TryGetValue("SUMMARY", out var s) ? Unescape(s.Value) : "";
        var value = start.Value.Trim();

        if (value.Length == 8)
        {
            if (!DateTime.TryParseExact(value, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)) return null;
            return new CalendarEvent(uid.Value.Trim(), title.Trim(), DateOnly.FromDateTime(date), null, etag, href);
        }

        var utc = value.EndsWith('Z');
        if (!DateTime.TryParseExact(utc ? value[..^1] : value, "yyyyMMdd'T'HHmmss",
                CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt)) return null;
        if (utc) dt = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(dt, DateTimeKind.Utc), zone);

        var (day, part) = TimeOfDayBuckets.FromStart(dt);
        return new CalendarEvent(uid.Value.Trim(), title.Trim(), day, part, etag, href);
    }

    // Unfolds continuation lines (a leading space or tab) and splits NAME;PARAMS:VALUE.
    private static IEnumerable<(string Name, string Params, string Value)> Lines(string ics)
    {
        var unfolded = new List<string>();
        foreach (var raw in ics.Replace("\r\n", "\n").Split('\n'))
        {
            if (raw.Length > 0 && (raw[0] == ' ' || raw[0] == '\t') && unfolded.Count > 0) unfolded[^1] += raw[1..];
            else unfolded.Add(raw);
        }

        foreach (var line in unfolded)
        {
            var colon = line.IndexOf(':');
            if (colon <= 0) continue;
            var head = line[..colon];
            var semi = head.IndexOf(';');
            var name = (semi < 0 ? head : head[..semi]).ToUpperInvariant();
            var parameters = semi < 0 ? "" : head[(semi + 1)..];
            yield return (name, parameters, line[(colon + 1)..]);
        }
    }

    private static string Unescape(string v)
    {
        var sb = new StringBuilder();
        for (var i = 0; i < v.Length; i++)
        {
            if (v[i] == '\\' && i + 1 < v.Length)
            {
                i++;
                sb.Append(v[i] is 'n' or 'N' ? '\n' : v[i]);
            }
            else sb.Append(v[i]);
        }
        return sb.ToString();
    }
}
