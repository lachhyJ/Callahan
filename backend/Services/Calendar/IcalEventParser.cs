using System.Globalization;
using System.Text;

namespace Callahan.Api.Services.Calendar;

// A deliberately small iCalendar reader: just the VEVENT fields the planner uses.
// Parse skips recurring events (RRULE) - the Training calendar holds one-off
// sessions, and a recurring master would only show on its first date, which is
// worse than not showing. ParseInRange, for the read-only club calendar, expands
// them instead: iCloud ignores CalDAV's server-side expand and returns the series.
// Cancelled events are skipped by both.
public static class IcalEventParser
{
    private sealed class Props : Dictionary<string, (string Params, string Value)>
    {
        public List<string> ExDates { get; } = [];
    }

    public static IReadOnlyList<CalendarEvent> Parse(string ics, TimeZoneInfo userZone, string? etag = null, string? href = null) =>
        Events(ics)
            .Where(p => !p.ContainsKey("RRULE"))
            .Select(p => Build(p, userZone, etag, href, ics))
            .OfType<CalendarEvent>()
            .ToList();

    // Every occurrence that starts on a training day from `from` to `to`, one event per
    // date for a recurring series. A series whose rule this reader doesn't understand
    // (anything but daily or weekly) is skipped and counted in `unsupported`.
    public static IReadOnlyList<CalendarEvent> ParseInRange(
        string ics, TimeZoneInfo userZone, DateOnly from, DateOnly to, out int unsupported, string? etag = null, string? href = null)
    {
        unsupported = 0;
        var all = Events(ics);
        var result = new List<CalendarEvent>();

        // A moved or cancelled single date of a series arrives as its own VEVENT with the
        // series' UID and a RECURRENCE-ID naming the date it replaces.
        var replaced = all
            .Where(p => p.ContainsKey("RECURRENCE-ID") && p.ContainsKey("UID"))
            .Select(p => (Uid: p["UID"].Value.Trim(), Start: Start(p["RECURRENCE-ID"].Value, userZone)))
            .Where(r => r.Start is not null)
            .Select(r => (r.Uid, r.Start!.Value.Date))
            .ToHashSet();

        foreach (var p in all)
        {
            if (!p.TryGetValue("RRULE", out var rule))
            {
                if (Build(p, userZone, etag, href, ics) is { } single && single.Day >= from && single.Day <= to) result.Add(single);
                continue;
            }

            if (Cancelled(p) || !p.TryGetValue("UID", out var uid) || !p.TryGetValue("DTSTART", out var dtstart)) continue;
            if (Start(dtstart.Value, userZone) is not { } first) continue;
            var allDay = dtstart.Value.Trim().Length == 8;
            var title = p.TryGetValue("SUMMARY", out var s) ? Unescape(s.Value).Trim() : "";
            var excluded = p.ExDates
                .SelectMany(v => v.Split(','))
                .Select(v => Start(v, userZone))
                .OfType<DateTime>()
                .Select(d => d.Date)
                .ToHashSet();

            var starts = Occurrences(rule.Value, first, from.AddDays(-1).ToDateTime(TimeOnly.MinValue), to.AddDays(2).ToDateTime(TimeOnly.MinValue));
            if (starts is null) { unsupported++; continue; }

            foreach (var start in starts)
            {
                if (excluded.Contains(start.Date) || replaced.Contains((uid.Value.Trim(), start.Date))) continue;
                var (day, part) = allDay ? (DateOnly.FromDateTime(start), null) : Bucket(start);
                if (day < from || day > to) continue;
                result.Add(new CalendarEvent(uid.Value.Trim(), title, day, part, etag, href, ics));
            }
        }

        return result;
    }

    private static (DateOnly Day, TimeOfDay? Part) Bucket(DateTime start) => TimeOfDayBuckets.FromStart(start);

    // The starts of a daily or weekly series that fall in [windowStart, windowEnd), each
    // at the first start's wall-clock time - a 7pm training stays 7pm across a daylight
    // saving change, as it does in Calendar. Null for any other rule. COUNT counts from
    // the first occurrence, not from the window, as RFC 5545 has it.
    private static List<DateTime>? Occurrences(string rule, DateTime first, DateTime windowStart, DateTime windowEnd)
    {
        var parts = rule.Split(';', StringSplitOptions.RemoveEmptyEntries)
            .Select(x => x.Split('=', 2))
            .Where(x => x.Length == 2)
            .ToDictionary(x => x[0].Trim().ToUpperInvariant(), x => x[1].Trim().ToUpperInvariant());

        var freq = parts.GetValueOrDefault("FREQ");
        if (freq is not ("DAILY" or "WEEKLY")) return null;
        var interval = parts.TryGetValue("INTERVAL", out var i) && int.TryParse(i, out var n) && n > 0 ? n : 1;
        int? count = parts.TryGetValue("COUNT", out var c) && int.TryParse(c, out var cn) ? cn : null;
        DateTime? until = parts.TryGetValue("UNTIL", out var u) ? UntilDate(u) : null;

        IReadOnlyList<DayOfWeek> days = [first.DayOfWeek];
        if (parts.TryGetValue("BYDAY", out var byday))
        {
            // Only plain weekdays; "1MO" (the first Monday of a month) belongs to monthly rules.
            var parsed = byday.Split(',').Select(WeekDay).ToList();
            if (parsed.Any(d => d is null)) return null;
            days = parsed.Select(d => d!.Value).ToList();
        }

        var result = new List<DateTime>();
        var seen = 0;
        // Week-by-week (or day-by-day) from the first start; capped so a malformed rule
        // can't spin. 3000 steps is over fifty years of a weekly series.
        var periodStart = freq == "WEEKLY" ? first.Date.AddDays(-(((int)first.DayOfWeek + 6) % 7)) : first.Date;
        for (var step = 0; step < 3000; step++)
        {
            IEnumerable<DateTime> candidates = freq == "WEEKLY"
                ? days.Select(d => periodStart.AddDays(((int)d + 6) % 7)).OrderBy(d => d)
                : [periodStart];

            foreach (var date in candidates)
            {
                var start = date + first.TimeOfDay;
                if (start < first) continue;
                if (until is { } last && date > last) return result;
                if (count is { } max && seen >= max) return result;
                seen++;
                if (start >= windowEnd) return result;
                if (start >= windowStart) result.Add(start);
            }

            periodStart = periodStart.AddDays(freq == "WEEKLY" ? 7 * interval : interval);
        }
        return result;
    }

    // UNTIL is compared by date: a series that ends "on" a day includes that day.
    private static DateTime? UntilDate(string v)
    {
        var digits = v.Length >= 8 ? v[..8] : v;
        return DateTime.TryParseExact(digits, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) ? d : null;
    }

    private static DayOfWeek? WeekDay(string v) => v.Trim() switch
    {
        "MO" => DayOfWeek.Monday,
        "TU" => DayOfWeek.Tuesday,
        "WE" => DayOfWeek.Wednesday,
        "TH" => DayOfWeek.Thursday,
        "FR" => DayOfWeek.Friday,
        "SA" => DayOfWeek.Saturday,
        "SU" => DayOfWeek.Sunday,
        _ => null
    };

    private static List<Props> Events(string ics)
    {
        var events = new List<Props>();
        Props? current = null;
        var depth = 0;

        foreach (var (name, parameters, value) in Lines(ics))
        {
            if (name == "BEGIN" && value == "VEVENT") { current = new(); depth = 0; continue; }
            if (name == "END" && value == "VEVENT")
            {
                if (current is not null) events.Add(current);
                current = null;
                continue;
            }
            if (current is null) continue;
            if (name == "BEGIN") { depth++; continue; }
            if (name == "END") { depth--; continue; }
            // Properties of a component nested in the event (a VALARM) aren't the event's.
            if (depth > 0) continue;
            if (name == "EXDATE") { current.ExDates.Add(value); continue; }
            // First occurrence wins.
            current.TryAdd(name, (parameters, value));
        }

        return events;
    }

    private static bool Cancelled(Props p) =>
        p.TryGetValue("STATUS", out var status) && status.Value.Equals("CANCELLED", StringComparison.OrdinalIgnoreCase);

    // An event's start as wall-clock time: a date, a floating or zoned time as written
    // (see CalendarEvent on why a TZID is ignored), or a UTC time read in the user's zone.
    private static DateTime? Start(string raw, TimeZoneInfo zone)
    {
        var value = raw.Trim();
        if (value.Length == 8)
        {
            return DateTime.TryParseExact(value, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
                ? date : null;
        }

        var utc = value.EndsWith('Z');
        if (!DateTime.TryParseExact(utc ? value[..^1] : value, "yyyyMMdd'T'HHmmss",
                CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt)) return null;
        return utc ? TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(dt, DateTimeKind.Utc), zone) : dt;
    }

    private static CalendarEvent? Build(Props p, TimeZoneInfo zone, string? etag, string? href, string raw)
    {
        if (!p.TryGetValue("UID", out var uid) || string.IsNullOrWhiteSpace(uid.Value)) return null;
        if (p.ContainsKey("RRULE") || Cancelled(p)) return null;
        if (!p.TryGetValue("DTSTART", out var dtstart) || Start(dtstart.Value, zone) is not { } start) return null;

        var title = p.TryGetValue("SUMMARY", out var s) ? Unescape(s.Value) : "";
        if (dtstart.Value.Trim().Length == 8)
        {
            return new CalendarEvent(uid.Value.Trim(), title.Trim(), DateOnly.FromDateTime(start), null, etag, href, raw);
        }

        var (day, part) = TimeOfDayBuckets.FromStart(start);
        return new CalendarEvent(uid.Value.Trim(), title.Trim(), day, part, etag, href, raw);
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
