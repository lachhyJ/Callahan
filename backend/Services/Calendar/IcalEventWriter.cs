using System.Globalization;
using System.Text;
using System.Xml;

namespace Callahan.Api.Services.Calendar;

// Writes the two things the planner puts in the Training calendar: a new event, and
// a changed time on an event that already exists. Times are written as wall-clock
// time, floating (no zone), so "morning" stays morning wherever the phone is.
public static class IcalEventWriter
{
    private const string Stamp = "yyyyMMdd'T'HHmmss";

    public static string NewUid() => $"callahan-{Guid.NewGuid():N}@callahan";

    public static string Create(string uid, string title, DateTime start, DateTime nowUtc) =>
        string.Join("\r\n",
            "BEGIN:VCALENDAR",
            "VERSION:2.0",
            "PRODID:-//Callahan//Planner//EN",
            "BEGIN:VEVENT",
            $"UID:{uid}",
            $"DTSTAMP:{nowUtc.ToString(Stamp, CultureInfo.InvariantCulture)}Z",
            $"DTSTART:{start.ToString(Stamp, CultureInfo.InvariantCulture)}",
            $"DTEND:{start.Add(TimeOfDayBuckets.DefaultDuration).ToString(Stamp, CultureInfo.InvariantCulture)}",
            $"SUMMARY:{Escape(title)}",
            "END:VEVENT",
            "END:VCALENDAR") + "\r\n";

    // Moves the event to newStart and keeps its length. Every other line is left alone,
    // including notes, alarms and a VTIMEZONE: an event made in Calendar may carry
    // things Callahan has no business rewriting. An event with a TZID keeps it (the
    // wall-clock time is what changes); a UTC or floating one becomes floating; an
    // all-day one becomes timed. Only the first VEVENT is touched.
    public static string Reschedule(string ics, DateTime newStart, DateTime nowUtc)
    {
        var lines = LogicalLines(ics);

        var inEvent = false;
        var depth = 0; // nested components inside the VEVENT (VALARM)
        int startAt = -1, endAt = -1, durationAt = -1, stampAt = -1, eventEnd = -1;
        for (var i = 0; i < lines.Count; i++)
        {
            var text = lines[i];
            if (!inEvent)
            {
                if (text.Equals("BEGIN:VEVENT", StringComparison.OrdinalIgnoreCase)) inEvent = true;
                continue;
            }
            if (text.StartsWith("BEGIN:", StringComparison.OrdinalIgnoreCase)) { depth++; continue; }
            if (text.StartsWith("END:", StringComparison.OrdinalIgnoreCase))
            {
                if (depth > 0) { depth--; continue; }
                eventEnd = i;
                break;
            }
            if (depth > 0) continue;
            var name = NameOf(text);
            if (name == "DTSTART" && startAt < 0) startAt = i;
            else if (name == "DTEND" && endAt < 0) endAt = i;
            else if (name == "DURATION" && durationAt < 0) durationAt = i;
            else if (name == "DTSTAMP" && stampAt < 0) stampAt = i;
        }
        if (eventEnd < 0 || startAt < 0) throw new FormatException("The event has no start time to change.");

        var duration = DurationOf(lines, startAt, endAt, durationAt) ?? TimeOfDayBuckets.DefaultDuration;
        var tzid = TzidOf(lines[startAt]);
        var zone = tzid is null ? "" : $";TZID={tzid}";

        var newLines = new List<string>();
        for (var i = 0; i < lines.Count; i++)
        {
            if (i == startAt) newLines.Add($"DTSTART{zone}:{newStart.ToString(Stamp, CultureInfo.InvariantCulture)}");
            else if (i == endAt) newLines.Add($"DTEND{zone}:{newStart.Add(duration).ToString(Stamp, CultureInfo.InvariantCulture)}");
            else if (i == durationAt) continue; // replaced by an explicit DTEND
            else if (i == stampAt) newLines.Add($"DTSTAMP:{nowUtc.ToString(Stamp, CultureInfo.InvariantCulture)}Z");
            else newLines.Add(lines[i]);

            // An event with a DURATION and no DTEND still needs an end after the move.
            if (i == startAt && endAt < 0)
            {
                newLines.Add($"DTEND{zone}:{newStart.Add(duration).ToString(Stamp, CultureInfo.InvariantCulture)}");
            }
        }

        return string.Join("\r\n", newLines) + "\r\n";
    }

    // Unfolds continuation lines so each property is one string; the lines Callahan
    // does not change are written back unfolded, which every calendar server accepts.
    private static List<string> LogicalLines(string ics)
    {
        var result = new List<string>();
        foreach (var raw in ics.Replace("\r\n", "\n").Split('\n'))
        {
            if (raw.Length > 0 && (raw[0] == ' ' || raw[0] == '\t') && result.Count > 0) result[^1] += raw[1..];
            else if (raw.Length > 0) result.Add(raw);
        }
        return result;
    }

    private static string NameOf(string line)
    {
        var end = line.IndexOfAny([':', ';']);
        return (end < 0 ? line : line[..end]).ToUpperInvariant();
    }

    private static string? TzidOf(string line)
    {
        var head = line[..Math.Max(0, line.IndexOf(':'))];
        foreach (var part in head.Split(';').Skip(1))
        {
            if (part.StartsWith("TZID=", StringComparison.OrdinalIgnoreCase)) return part[5..].Trim('"');
        }
        return null;
    }

    private static DateTime? ValueOf(string line)
    {
        var value = line[(line.IndexOf(':') + 1)..].Trim();
        if (value.EndsWith('Z')) value = value[..^1];
        return DateTime.TryParseExact(value, Stamp, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt) ? dt : null;
    }

    private static TimeSpan? DurationOf(List<string> lines, int startAt, int endAt, int durationAt)
    {
        if (endAt >= 0 && ValueOf(lines[startAt]) is { } s && ValueOf(lines[endAt]) is { } e && e > s) return e - s;
        if (durationAt >= 0)
        {
            try { return XmlConvert.ToTimeSpan(lines[durationAt][(lines[durationAt].IndexOf(':') + 1)..].Trim()); }
            catch (FormatException) { }
        }
        return null;
    }

    private static string Escape(string v) =>
        new StringBuilder(v).Replace("\\", "\\\\").Replace(";", "\\;").Replace(",", "\\,").Replace("\n", "\\n").ToString();
}
