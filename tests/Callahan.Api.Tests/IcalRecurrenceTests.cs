using Callahan.Api.Services.Calendar;

namespace Callahan.Api.Tests;

// The club calendar's weekly series, expanded here because iCloud ignores CalDAV's
// expand and returns each series once. Week of Mon 23 Nov 2026.
public class IcalRecurrenceTests
{
    private static readonly TimeZoneInfo Melbourne = TimeZoneInfo.FindSystemTimeZoneById("Australia/Melbourne");
    private static readonly DateOnly Mon = new(2026, 11, 23);

    // iCloud's shape: a VTIMEZONE (whose own RRULEs are daylight-saving rules, not the
    // event's) and a zoned DTSTART.
    private static string Series(string rule, string start = "20260903T190000", string extra = "", string more = "") => $"""
        BEGIN:VCALENDAR
        BEGIN:VTIMEZONE
        TZID:Australia/Melbourne
        BEGIN:DAYLIGHT
        DTSTART:20081005T020000
        RRULE:FREQ=YEARLY;BYMONTH=10;BYDAY=1SU
        END:DAYLIGHT
        END:VTIMEZONE
        BEGIN:VEVENT
        UID:chilly
        SUMMARY:Chilly training
        DTSTART;TZID=Australia/Melbourne:{start}
        RRULE:{rule}
        {extra}
        BEGIN:VALARM
        TRIGGER:-PT30M
        SUMMARY:Alarm
        END:VALARM
        END:VEVENT
        {more}
        END:VCALENDAR
        """;

    private static List<(DateOnly Day, TimeOfDay? Part, string Title)> Expand(string ics) =>
        IcalEventParser.ParseInRange(ics, Melbourne, Mon, Mon.AddDays(6), out _)
            .Select(e => (e.Day, e.Part, e.Title)).ToList();

    [Fact]
    public void AWeeklySeriesShowsOnItsDayThisWeek()
    {
        // Started on a Thursday in September; this week's Thursday is 26 Nov.
        Assert.Equal([(new DateOnly(2026, 11, 26), (TimeOfDay?)TimeOfDay.Evening, "Chilly training")],
            Expand(Series("FREQ=WEEKLY")));
    }

    [Fact]
    public void BydayGivesEveryListedDay()
    {
        Assert.Equal([new DateOnly(2026, 11, 24), new DateOnly(2026, 11, 26)],
            Expand(Series("FREQ=WEEKLY;BYDAY=TU,TH")).Select(e => e.Day));
    }

    [Fact]
    public void AFortnightlySeriesSkipsItsOffWeek()
    {
        // 3 Sep + 12 weeks = 26 Nov (on); 3 Sep + 11 weeks = 19 Nov (off).
        Assert.Single(Expand(Series("FREQ=WEEKLY;INTERVAL=2")));
        Assert.Empty(Expand(Series("FREQ=WEEKLY;INTERVAL=2", start: "20260910T190000")));
    }

    [Fact]
    public void AnEndedSeriesIsGone()
    {
        Assert.Empty(Expand(Series("FREQ=WEEKLY;UNTIL=20261120T085959Z")));
        Assert.Empty(Expand(Series("FREQ=WEEKLY;COUNT=12"))); // 3 Sep .. 19 Nov
        Assert.Single(Expand(Series("FREQ=WEEKLY;COUNT=13")));
    }

    [Fact]
    public void AnExcludedDateIsSkipped()
    {
        Assert.Empty(Expand(Series("FREQ=WEEKLY", extra: "EXDATE;TZID=Australia/Melbourne:20261126T190000")));
    }

    [Fact]
    public void AMovedDateShowsWhereItWasMovedTo()
    {
        var moved = """
            BEGIN:VEVENT
            UID:chilly
            RECURRENCE-ID;TZID=Australia/Melbourne:20261126T190000
            DTSTART;TZID=Australia/Melbourne:20261127T080000
            SUMMARY:Chilly training (moved)
            END:VEVENT
            """;
        Assert.Equal([(new DateOnly(2026, 11, 27), (TimeOfDay?)TimeOfDay.Morning, "Chilly training (moved)")],
            Expand(Series("FREQ=WEEKLY", more: moved)));
    }

    [Fact]
    public void ACancelledDateIsSkipped()
    {
        var cancelled = """
            BEGIN:VEVENT
            UID:chilly
            RECURRENCE-ID;TZID=Australia/Melbourne:20261126T190000
            DTSTART;TZID=Australia/Melbourne:20261126T190000
            STATUS:CANCELLED
            SUMMARY:Chilly training
            END:VEVENT
            """;
        Assert.Empty(Expand(Series("FREQ=WEEKLY", more: cancelled)));
    }

    [Fact]
    public void AMonthlySeriesIsCountedAsUnsupportedNotGuessed()
    {
        IcalEventParser.ParseInRange(Series("FREQ=MONTHLY;BYDAY=4TH"), Melbourne, Mon, Mon.AddDays(6), out var unsupported);
        Assert.Equal(1, unsupported);
    }

    [Theory]
    [InlineData("FREQ=WEEKLY;FREQ=WEEKLY")]
    [InlineData("FREQ=WEEKLY;BYDAY=TH,TH,TH,TH,TH,TH,TH,TH")]
    public void ARepeatedPartOfARuleIsReadOnce(string rule) => Assert.Single(Expand(Series(rule)));

    [Theory]
    [InlineData("FREQ=WEEKLY;INTERVAL=2147483647")]
    [InlineData("FREQ=DAILY;INTERVAL=99999999")]
    [InlineData("FREQ=WEEKLY;INTERVAL=0")]
    public void AnAbsurdIntervalIsUnsupportedNotAnError(string rule)
    {
        IcalEventParser.ParseInRange(Series(rule), Melbourne, Mon, Mon.AddDays(6), out var unsupported);
        Assert.Equal(1, unsupported);
    }

    [Fact]
    public void ADateNearTheEndOfTimeIsNotAnError()
    {
        IcalEventParser.ParseInRange(Series("FREQ=DAILY", start: "99991231T190000"), Melbourne, Mon, Mon.AddDays(6), out var unsupported);
        Assert.Equal(0, unsupported);
    }

    [Fact]
    public void TheTrainingCalendarsParseStillSkipsSeries()
    {
        Assert.Empty(IcalEventParser.Parse(Series("FREQ=WEEKLY"), Melbourne));
    }
}
