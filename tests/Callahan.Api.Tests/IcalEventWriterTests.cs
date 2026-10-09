using Callahan.Api.Services.Calendar;

namespace Callahan.Api.Tests;

// What Callahan writes into the Training calendar: a new event, and a changed time on
// one that already exists. The point of the second is that it changes the time and
// nothing else.
public class IcalEventWriterTests
{
    private static readonly TimeZoneInfo Melbourne = TimeZoneInfo.FindSystemTimeZoneById("Australia/Melbourne");
    private static readonly DateTime Now = new(2026, 10, 7, 4, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Thu = new(2026, 10, 8, 18, 0, 0);

    private static string Event(params string[] lines) =>
        "BEGIN:VCALENDAR\r\nVERSION:2.0\r\nBEGIN:VEVENT\r\n" + string.Join("\r\n", lines) + "\r\nEND:VEVENT\r\nEND:VCALENDAR\r\n";

    private static string[] Props(string ics) =>
        ics.Replace("\r\n", "\n").Split('\n', StringSplitOptions.RemoveEmptyEntries);

    [Theory]
    [InlineData(TimeOfDay.Morning, 7)]
    [InlineData(TimeOfDay.Arvo, 12)]
    [InlineData(TimeOfDay.Evening, 18)]
    public void ANewEventReadsBackOnTheDayAndPartItWasWrittenFor(TimeOfDay part, int hour)
    {
        var uid = IcalEventWriter.NewUid();
        var start = TimeOfDayBuckets.DefaultStart(new DateOnly(2026, 10, 8), part);
        var ics = IcalEventWriter.Create(uid, "Gym 2", start, Now);

        var e = Assert.Single(IcalEventParser.Parse(ics, Melbourne));
        Assert.Equal((uid, "Gym 2", new DateOnly(2026, 10, 8), (TimeOfDay?)part), (e.Uid, e.Title, e.Day, e.Part));
        Assert.Contains($"DTSTART:20261008T{hour:00}0000", ics);
        Assert.Contains($"DTEND:20261008T{hour:00}0000".Replace($"T{hour:00}0000", $"T{start.Add(TimeOfDayBuckets.DefaultDuration):HHmm}00"), ics);
    }

    [Fact]
    public void ANewEventsUidIsOursAndUnique()
    {
        var a = IcalEventWriter.NewUid();
        Assert.StartsWith("callahan-", a);
        Assert.EndsWith("@callahan", a);
        Assert.NotEqual(a, IcalEventWriter.NewUid());
    }

    [Fact]
    public void ATitleWithSpecialCharactersIsEscapedAndReadsBackIntact()
    {
        var ics = IcalEventWriter.Create("u", "Gym 1, upper; heavy", Thu, Now);
        Assert.Equal("Gym 1, upper; heavy", Assert.Single(IcalEventParser.Parse(ics, Melbourne)).Title);
    }

    [Fact]
    public void RescheduleChangesTheTimeAndKeepsTheLength()
    {
        var ics = Event("UID:a", "SUMMARY:Gym 1", "DTSTART:20261006T070000", "DTEND:20261006T080000");

        var moved = IcalEventWriter.Reschedule(ics, Thu, Now);

        Assert.Contains("DTSTART:20261008T180000", moved);
        Assert.Contains("DTEND:20261008T190000", moved); // an hour, as it was, not the 90-minute default
        var e = Assert.Single(IcalEventParser.Parse(moved, Melbourne));
        Assert.Equal((new DateOnly(2026, 10, 8), (TimeOfDay?)TimeOfDay.Evening), (e.Day, e.Part));
    }

    [Fact]
    public void RescheduleLeavesNotesAlarmsAndEverythingElseAlone()
    {
        var ics = Event("UID:a", "SUMMARY:Gym 1", "DESCRIPTION:Heavy trap bar", "LOCATION:Fitness First",
            "DTSTART:20261006T070000", "DTEND:20261006T083000",
            "BEGIN:VALARM", "ACTION:DISPLAY", "DESCRIPTION:Reminder", "TRIGGER:-PT15M", "END:VALARM");

        var moved = Props(IcalEventWriter.Reschedule(ics, Thu, Now));
        var before = Props(ics);

        foreach (var kept in new[] { "UID:a", "SUMMARY:Gym 1", "DESCRIPTION:Heavy trap bar", "LOCATION:Fitness First",
            "BEGIN:VALARM", "ACTION:DISPLAY", "DESCRIPTION:Reminder", "TRIGGER:-PT15M", "END:VALARM" })
        {
            Assert.Contains(kept, moved);
        }
        Assert.Equal(before.Length, moved.Length);
    }

    [Fact]
    public void AnAlarmsOwnDurationIsNeitherTheEventsLengthNorRemoved()
    {
        // A repeating alarm has a DURATION of its own: the gap between repeats.
        var ics = Event("UID:a", "SUMMARY:Gym 1", "DTSTART:20261006T070000", "DTEND:20261006T080000",
            "BEGIN:VALARM", "ACTION:DISPLAY", "TRIGGER:-PT15M", "REPEAT:2", "DURATION:PT5M", "END:VALARM");

        var moved = IcalEventWriter.Reschedule(ics, Thu, Now);

        Assert.Contains("DURATION:PT5M", moved);       // the alarm still repeats every 5 minutes
        Assert.Contains("DTEND:20261008T190000", moved); // the event is still an hour long
    }

    [Fact]
    public void AnAlarmsDurationIsNotMistakenForTheEventsWhenTheEventHasNoEnd()
    {
        var ics = Event("UID:a", "SUMMARY:Gym 1", "DTSTART:20261006T070000",
            "BEGIN:VALARM", "ACTION:DISPLAY", "TRIGGER:-PT15M", "REPEAT:2", "DURATION:PT5M", "END:VALARM");

        var moved = IcalEventWriter.Reschedule(ics, Thu, Now);

        Assert.Contains("DURATION:PT5M", moved);
        Assert.Contains("DTEND:20261008T193000", moved); // the 90-minute default, not 5 minutes
    }

    [Fact]
    public void ATimeZoneOnTheEventIsKept()
    {
        var ics = Event("UID:a", "SUMMARY:Gym 1", "DTSTART;TZID=Australia/Melbourne:20261006T070000", "DTEND;TZID=Australia/Melbourne:20261006T080000");

        var moved = IcalEventWriter.Reschedule(ics, Thu, Now);

        Assert.Contains("DTSTART;TZID=Australia/Melbourne:20261008T180000", moved);
        Assert.Contains("DTEND;TZID=Australia/Melbourne:20261008T190000", moved);
    }

    [Fact]
    public void AUtcEventBecomesFloating()
    {
        var ics = Event("UID:a", "SUMMARY:Gym 1", "DTSTART:20261005T200000Z", "DTEND:20261005T210000Z");
        var moved = IcalEventWriter.Reschedule(ics, Thu, Now);
        Assert.Contains("DTSTART:20261008T180000", moved);
        Assert.DoesNotContain("20261008T180000Z", moved);
    }

    [Fact]
    public void ADurationIsReplacedByAnExplicitEnd()
    {
        var ics = Event("UID:a", "SUMMARY:Gym 1", "DTSTART:20261006T070000", "DURATION:PT45M");
        var moved = IcalEventWriter.Reschedule(ics, Thu, Now);
        Assert.Contains("DTEND:20261008T184500", moved);
        Assert.DoesNotContain("DURATION", moved);
    }

    [Fact]
    public void AnAllDayEventBecomesTimedAtTheDefaultLength()
    {
        var ics = Event("UID:a", "SUMMARY:Gym 1", "DTSTART;VALUE=DATE:20261006", "DTEND;VALUE=DATE:20261007");
        var moved = IcalEventWriter.Reschedule(ics, Thu, Now);
        Assert.Contains("DTSTART:20261008T180000", moved);
        Assert.Contains("DTEND:20261008T193000", moved);
        Assert.DoesNotContain("VALUE=DATE", moved);
    }

    [Fact]
    public void AnEventWithNoStartCannotBeMoved()
    {
        Assert.Throws<FormatException>(() => IcalEventWriter.Reschedule(Event("UID:a", "SUMMARY:Gym 1"), Thu, Now));
    }

    [Fact]
    public void OnlyTheFirstEventInAResourceIsTouched()
    {
        var ics = "BEGIN:VCALENDAR\r\nBEGIN:VEVENT\r\nUID:a\r\nDTSTART:20261006T070000\r\nDTEND:20261006T080000\r\nEND:VEVENT\r\n"
            + "BEGIN:VEVENT\r\nUID:b\r\nRECURRENCE-ID:20261013T070000\r\nDTSTART:20261013T070000\r\nDTEND:20261013T080000\r\nEND:VEVENT\r\nEND:VCALENDAR\r\n";
        var moved = IcalEventWriter.Reschedule(ics, Thu, Now);
        Assert.Contains("DTSTART:20261008T180000", moved);
        Assert.Contains("DTSTART:20261013T070000", moved);
    }
}
