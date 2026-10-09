namespace Callahan.Api.Services.Calendar;

// Thrown when the calendar is configured but can't be read: wrong password, no
// network, no calendar of that name. The Plan page shows its normal week with a
// banner instead of failing.
public class CalendarUnavailableException(string message, Exception? inner = null) : Exception(message, inner);

// Thrown when a write is refused because the event changed since it was read (an
// If-Match that no longer matches). The caller re-reads and shows what is there now,
// rather than overwriting an edit made in the Calendar app.
public class CalendarConflictException() : Exception("Your calendar changed since this page loaded. It has been refreshed.");

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

    // Writes. Only ever to the Training calendar. A new event is written at the part
    // of the day's default start; a move keeps the event's length and everything else
    // about it; a delete removes the event. Moves and deletes carry the ETag the event
    // was read with, so an edit made in Calendar in between is not overwritten.
    Task CreateEventAsync(string title, DateOnly day, TimeOfDay part, CancellationToken ct);

    Task MoveEventAsync(CalendarEvent existing, DateOnly day, TimeOfDay part, CancellationToken ct);

    Task DeleteEventAsync(CalendarEvent existing, CancellationToken ct);
}
