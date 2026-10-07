namespace Callahan.Api.Services.Calendar;

// One event from the Training calendar, reduced to what the planner uses: the
// training day it falls on and the part of the day. The start is wall-clock time in
// whatever zone the event was made in - "morning" means the same thing on the
// Seattle trip as at home, so a TZID is deliberately ignored. A UTC time (Z) is the
// exception: it has no wall clock of its own, so it is read in the user's zone.
public record CalendarEvent(
    string Uid,
    string Title,
    DateOnly Day,
    // Null for an all-day event, which has no part of the day.
    TimeOfDay? Part,
    string? ETag = null,
    string? Href = null);
