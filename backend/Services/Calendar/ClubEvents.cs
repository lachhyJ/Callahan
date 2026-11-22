using System.Text.RegularExpressions;

namespace Callahan.Api.Services.Calendar;

// Picks the club sessions out of the club calendar, which holds everything else in
// life too. A club session is any event with "training" or "pod" as a whole word in
// its title - "Chilly training", "Chilly pod", "pod" - so "podcast" and "trainingday"
// don't count. Whole words rather than a prefix because the club name comes first
// and varies.
public static partial class ClubEvents
{
    public static readonly string[] Words = ["training", "pod"];

    public static bool IsClubSession(string title) =>
        WordPattern().Split(title.ToLowerInvariant()).Any(w => Words.Contains(w));

    // The week's club sessions, earliest first. Never throws for a calendar that can't
    // be read: this is context beside the plan, and the plan works without it.
    public static async Task<List<CalendarEvent>> ForWeekAsync(
        ICalendarClient? calendar, DateOnly start, DateOnly end, ILogger? logger, CancellationToken ct)
    {
        if (calendar is not { IsConfigured: true }) return [];
        try
        {
            var events = await calendar.GetClubEventsAsync(start, end, ct);
            return events
                .Where(e => e.Day >= start && e.Day <= end && IsClubSession(e.Title))
                .OrderBy(e => e.Day).ThenBy(e => e.Part)
                .ToList();
        }
        catch (CalendarUnavailableException ex)
        {
            logger?.LogWarning("Couldn't read the club calendar: {Message}", ex.Message);
            return [];
        }
    }

    [GeneratedRegex(@"[^\p{L}\p{N}]+")]
    private static partial Regex WordPattern();
}
