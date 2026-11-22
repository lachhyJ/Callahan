using Callahan.Api.Services.Calendar;

namespace Callahan.Api.Tests;

public class ClubEventsTests
{
    [Theory]
    [InlineData("Chilly training", true)]
    [InlineData("Chilly Training - Thursday", true)]
    [InlineData("pod", true)]
    [InlineData("Chilly pod", true)]
    [InlineData("Throws pod (Lucy)", true)]
    [InlineData("Training", true)]
    [InlineData("Podcast recording", false)]
    [InlineData("Trainingday", false)]
    [InlineData("Dentist", false)]
    [InlineData("", false)]
    public void AClubSessionHasTrainingOrPodAsAWholeWord(string title, bool expected) =>
        Assert.Equal(expected, ClubEvents.IsClubSession(title));

    // What a CalDAV server sends back for one date of an expanded weekly series: no
    // RRULE, a RECURRENCE-ID, and the start in UTC. 08:30Z on a Thursday in October
    // is 19:30 in Melbourne - an evening session on the same day.
    [Fact]
    public void AnExpandedInstanceOfAWeeklySeriesParsesToItsOwnDate()
    {
        const string ics = """
            BEGIN:VCALENDAR
            BEGIN:VEVENT
            UID:chilly-weekly
            RECURRENCE-ID:20261008T083000Z
            DTSTART:20261008T083000Z
            SUMMARY:Chilly training
            END:VEVENT
            END:VCALENDAR
            """;
        var melbourne = TimeZoneInfo.FindSystemTimeZoneById("Australia/Melbourne");

        var e = Assert.Single(IcalEventParser.Parse(ics, melbourne));

        Assert.Equal((new DateOnly(2026, 10, 8), (TimeOfDay?)TimeOfDay.Evening), (e.Day, e.Part));
    }
}
