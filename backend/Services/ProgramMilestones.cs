using System.Text.RegularExpressions;
using Callahan.Api.Services.Calendar;

namespace Callahan.Api.Services;

// The program's review points and test days (T0-T4) live as events in the Training
// calendar, because their dates are approximate and move when the club confirms - the
// calendar is where they get moved. The Dashboard shows the next one from there, so
// there is no second list of dates to keep in step.
public static partial class ProgramMilestones
{
    // How far ahead the Dashboard looks. Far enough to plan a test week around, near
    // enough that a line about something two months off isn't noise.
    public const int LookaheadDays = 28;

    // "T0 testing", "T2 testing + program review", "Program review". A gym or field
    // event never starts with T and a digit, and "review" alone is too loose.
    public static bool IsMilestone(string title) => MilestoneTitle().IsMatch(title.Trim());

    // The earliest milestone from today on, inclusive; ties go to the first listed.
    public static CalendarEvent? Next(IEnumerable<CalendarEvent> events, DateOnly today) =>
        events
            .Where(e => e.Day >= today && e.Day <= today.AddDays(LookaheadDays) && IsMilestone(e.Title))
            .OrderBy(e => e.Day)
            .FirstOrDefault();

    [GeneratedRegex(@"^T[0-4]\b|program review", RegexOptions.IgnoreCase)]
    private static partial Regex MilestoneTitle();
}
