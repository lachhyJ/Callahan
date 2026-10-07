namespace Callahan.Api.Services.Calendar;

// Thrown when the calendar is configured but can't be read: wrong password, no
// network, no calendar of that name. The Plan page shows its normal week with a
// banner instead of failing.
public class CalendarUnavailableException(string message, Exception? inner = null) : Exception(message, inner);

// The Training calendar. Behind an interface so the planner can be tested, and run
// locally, without iCloud.
public interface ICalendarClient
{
    // False when no credentials are set; the Plan page then works as it did before
    // the calendar existed.
    bool IsConfigured { get; }

    // Events near the two dates, inclusive. May include events a day outside them,
    // so callers filter on CalendarEvent.Day.
    Task<IReadOnlyList<CalendarEvent>> GetEventsAsync(DateOnly from, DateOnly to, CancellationToken ct);
}
