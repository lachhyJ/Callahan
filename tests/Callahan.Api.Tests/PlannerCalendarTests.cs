using Callahan.Api.Models;
using Callahan.Api.Services;
using Callahan.Api.Services.Calendar;

namespace Callahan.Api.Tests;

public class TimeOfDayBucketsTests
{
    private static readonly DateOnly Tue = new(2026, 10, 6);

    [Theory]
    [InlineData(3, 0, TimeOfDay.Morning)]   // Morning does not begin until the 3am cutoff
    [InlineData(7, 0, TimeOfDay.Morning)]
    [InlineData(11, 59, TimeOfDay.Morning)]
    [InlineData(12, 0, TimeOfDay.Arvo)]     // the arvo starts at midday
    [InlineData(15, 59, TimeOfDay.Arvo)]
    [InlineData(16, 0, TimeOfDay.Evening)]
    [InlineData(23, 30, TimeOfDay.Evening)]
    public void StartHourMapsToAPartOfTheDayOnThatDate(int hour, int minute, TimeOfDay expected)
    {
        var (day, part) = TimeOfDayBuckets.FromStart(Tue.ToDateTime(new TimeOnly(hour, minute)));
        Assert.Equal(Tue, day);
        Assert.Equal(expected, part);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 30)]
    [InlineData(2, 59)]
    public void AnEventBeforeTheCutoffIsThePreviousDaysEvening(int hour, int minute)
    {
        var (day, part) = TimeOfDayBuckets.FromStart(Tue.ToDateTime(new TimeOnly(hour, minute)));
        Assert.Equal(Tue.AddDays(-1), day);
        Assert.Equal(TimeOfDay.Evening, part);
    }

    [Theory]
    [InlineData(TimeOfDay.Morning, 7)]
    [InlineData(TimeOfDay.Arvo, 12)]
    [InlineData(TimeOfDay.Evening, 18)]
    public void WrittenStartsReadBackAsTheSamePart(TimeOfDay part, int hour)
    {
        var start = TimeOfDayBuckets.DefaultStart(Tue, part);
        Assert.Equal(hour, start.Hour);
        Assert.Equal((Tue, part), TimeOfDayBuckets.FromStart(start));
    }
}

public class IcalEventParserTests
{
    private static readonly TimeZoneInfo Melbourne = TimeZoneInfo.FindSystemTimeZoneById("Australia/Melbourne");

    private static string Ics(params string[] lines) =>
        "BEGIN:VCALENDAR\r\nVERSION:2.0\r\nBEGIN:VEVENT\r\n" + string.Join("\r\n", lines) + "\r\nEND:VEVENT\r\nEND:VCALENDAR\r\n";

    private static CalendarEvent One(params string[] lines) =>
        Assert.Single(IcalEventParser.Parse(Ics(lines), Melbourne));

    [Fact]
    public void AFloatingTimeIsReadAsWrittenOnItsOwnDay()
    {
        var e = One("UID:a", "SUMMARY:Gym 1", "DTSTART:20261006T070000");
        Assert.Equal(("a", "Gym 1", new DateOnly(2026, 10, 6), (TimeOfDay?)TimeOfDay.Morning), (e.Uid, e.Title, e.Day, e.Part));
    }

    [Fact]
    public void ATzidIsIgnoredBecauseMorningMeansTheSameWhereverYouAre()
    {
        var e = One("UID:a", "SUMMARY:Field 1", "DTSTART;TZID=America/Los_Angeles:20261006T180000");
        Assert.Equal(TimeOfDay.Evening, e.Part);
        Assert.Equal(new DateOnly(2026, 10, 6), e.Day);
    }

    [Fact]
    public void AUtcTimeIsShownInTheUsersZone()
    {
        // 02:00Z on the 6th is 13:00 AEDT that day: an arvo, not a 2am Evening.
        var e = One("UID:a", "SUMMARY:Gym 2", "DTSTART:20261006T020000Z");
        Assert.Equal((new DateOnly(2026, 10, 6), (TimeOfDay?)TimeOfDay.Arvo), (e.Day, e.Part));
    }

    [Fact]
    public void AnAllDayEventHasADayAndNoPartOfTheDay()
    {
        var e = One("UID:a", "SUMMARY:Gym 3", "DTSTART;VALUE=DATE:20261007");
        Assert.Equal((new DateOnly(2026, 10, 7), (TimeOfDay?)null), (e.Day, e.Part));
    }

    [Fact]
    public void AnEventJustAfterMidnightBelongsToThePreviousTrainingDay()
    {
        var e = One("UID:a", "SUMMARY:Run", "DTSTART:20261007T013000");
        Assert.Equal((new DateOnly(2026, 10, 6), (TimeOfDay?)TimeOfDay.Evening), (e.Day, e.Part));
    }

    [Fact]
    public void RecurringAndCancelledEventsAreSkipped()
    {
        Assert.Empty(IcalEventParser.Parse(Ics("UID:a", "SUMMARY:Gym 1", "DTSTART:20261006T070000", "RRULE:FREQ=WEEKLY"), Melbourne));
        Assert.Empty(IcalEventParser.Parse(Ics("UID:a", "SUMMARY:Gym 1", "DTSTART:20261006T070000", "STATUS:CANCELLED"), Melbourne));
    }

    [Fact]
    public void FoldedLinesAndEscapesInTheTitleAreUndone()
    {
        var e = One("UID:a", "SUMMARY:Gym 1\\, upper\\; he", " avy", "DTSTART:20261006T070000");
        Assert.Equal("Gym 1, upper; heavy", e.Title);
    }

    [Fact]
    public void AnAlarmInsideTheEventDoesNotOverwriteTheEventsOwnFields()
    {
        var e = One("UID:a", "SUMMARY:Gym 1", "DTSTART:20261006T070000",
            "BEGIN:VALARM", "ACTION:DISPLAY", "SUMMARY:Reminder", "TRIGGER:-PT15M", "END:VALARM");
        Assert.Equal("Gym 1", e.Title);
    }

    [Fact]
    public void AnEventWithoutAUidOrStartIsDropped()
    {
        Assert.Empty(IcalEventParser.Parse(Ics("SUMMARY:Gym 1", "DTSTART:20261006T070000"), Melbourne));
        Assert.Empty(IcalEventParser.Parse(Ics("UID:a", "SUMMARY:Gym 1"), Melbourne));
    }

    [Fact]
    public void ETagAndHrefAreCarriedThrough()
    {
        var e = Assert.Single(IcalEventParser.Parse(Ics("UID:a", "SUMMARY:Gym 1", "DTSTART:20261006T070000"), Melbourne, "\"etag\"", "/cal/a.ics"));
        Assert.Equal(("\"etag\"", "/cal/a.ics"), (e.ETag, e.Href));
    }
}

public class CalendarMatcherTests
{
    private static readonly DateOnly Tue = new(2026, 10, 6);

    private static CalendarEvent Ev(string uid, string title, int dayOffset = 0, TimeOfDay? part = TimeOfDay.Morning) =>
        new(uid, title, Tue.AddDays(dayOffset), part);

    private static readonly CalendarMatcher.SlotKey Gym1 = new(1, PlanSlotKind.Gym, "Gym 1", null);
    private static readonly CalendarMatcher.SlotKey Gym2 = new(2, PlanSlotKind.Gym, "Gym 2", null);
    private static readonly CalendarMatcher.SlotKey Field1 = new(3, PlanSlotKind.Field, "Field 1", null);
    private static readonly CalendarMatcher.SlotKey Aerobic = new(4, PlanSlotKind.Aerobic, "Aerobic - easy ride, or rest", null);

    [Fact]
    public void ATitleMatchesTheSlotNamedLikeIt_CaseAndSpacingInsensitive()
    {
        var r = CalendarMatcher.Match([Gym1, Gym2], [Ev("a", "  gym   2 ")]);
        Assert.Equal("a", r.BySlot[2].Uid);
        Assert.Empty(r.Linked); // found by title, so nothing to unlink
        Assert.False(r.BySlot.ContainsKey(1));
    }

    [Theory]
    [InlineData("Field 1")]
    [InlineData("field 1 - sprints")]
    public void ASlotWithASubtitleAnswersToItsShortName(string title)
    {
        // The seeded labels, as production has them.
        var field1 = new CalendarMatcher.SlotKey(3, PlanSlotKind.Field, "Field 1 - Acceleration & Jumps", null);
        var field2 = new CalendarMatcher.SlotKey(4, PlanSlotKind.Field, "Field 2 - Repeat Effort & COD", null);
        var r = CalendarMatcher.Match([field1, field2], [Ev("a", title)]);
        Assert.Equal("a", r.BySlot[3].Uid);
        Assert.False(r.BySlot.ContainsKey(4));
    }

    [Fact]
    public void ATitleMatchesOnAWordBoundaryNotAPrefix()
    {
        Assert.Equal("a", CalendarMatcher.Match([Gym1], [Ev("a", "Gym 1 - upper")]).BySlot[1].Uid);
        Assert.Empty(CalendarMatcher.Match([Gym1], [Ev("a", "Gym 10")]).BySlot);
    }

    [Theory]
    [InlineData("Aerobic run")]
    [InlineData("run")]
    [InlineData("Run - easy")]
    [InlineData("aerobic")]
    public void TheAerobicSlotAnswersToAerobicAndRun(string title)
    {
        Assert.Equal("a", CalendarMatcher.Match([Aerobic], [Ev("a", title)]).BySlot[4].Uid);
    }

    [Fact]
    public void RunningOnAnotherSessionsTitleDoesNotMatchAerobic()
    {
        Assert.Empty(CalendarMatcher.Match([Aerobic], [Ev("a", "Runway meeting")]).BySlot);
    }

    [Fact]
    public void AnUnrecognisedEventIsOtherAndAnUnmatchedSlotHasNoEvent()
    {
        var r = CalendarMatcher.Match([Gym1, Field1], [Ev("a", "Gym 1"), Ev("b", "Dinner")]);
        Assert.Equal(["b"], r.Other.Select(e => e.Uid));
        Assert.False(r.BySlot.ContainsKey(3));
    }

    [Fact]
    public void ALinkAlreadyMadeBeatsTheTitle()
    {
        // The event is called "Dinner" but was linked to Field 1 by hand.
        var linked = Field1 with { LinkedUid = "b" };
        var r = CalendarMatcher.Match([linked], [Ev("b", "Dinner")]);
        Assert.Equal("b", r.BySlot[3].Uid);
        Assert.Empty(r.Other);
        Assert.Equal([3], r.Linked);
    }

    [Fact]
    public void ALinkedEventIsNotAlsoMatchedByTitleToAnotherSlot()
    {
        // "Gym 2" is linked to Field 1, so Gym 2 must not grab it by name too.
        var r = CalendarMatcher.Match([Gym2, Field1 with { LinkedUid = "g2" }], [Ev("g2", "Gym 2")]);
        Assert.Equal("g2", r.BySlot[3].Uid);
        Assert.False(r.BySlot.ContainsKey(2));
    }

    [Fact]
    public void OneEventFillsOneSlotAndTwoEventsForTheSameSlotTakeTheEarlierFirst()
    {
        var r = CalendarMatcher.Match([Gym1], [Ev("late", "Gym 1", 2), Ev("early", "Gym 1", 0)]);
        Assert.Equal("early", r.BySlot[1].Uid);
        Assert.Equal(["late"], r.Other.Select(e => e.Uid));
    }
}

public class WeekPlanBuilderCalendarTests
{
    private static readonly DateOnly Monday = new(2026, 10, 5);
    private static readonly DateOnly Wednesday = Monday.AddDays(2);

    private static WeekPlanBuilder.SlotInput Slot(int id, PlanSlotKind kind, string label, int day, LowerBodyLoad? load = null, bool optional = false) =>
        new(id, day, 1, kind, label, optional, null, null, null, load);

    private static readonly WeekPlanBuilder.SlotInput Gym1 = Slot(1, PlanSlotKind.Gym, "Gym 1", 1, LowerBodyLoad.Heavy);
    private static readonly WeekPlanBuilder.SlotInput Field1 = Slot(2, PlanSlotKind.Field, "Field 1", 2);
    private static readonly WeekPlanBuilder.SlotInput Field2 = Slot(3, PlanSlotKind.Field, "Field 2", 4);
    private static readonly WeekPlanBuilder.SlotInput Jump = Slot(4, PlanSlotKind.Routine, "Jump Block", 1);
    private static readonly WeekPlanBuilder.SlotInput Rest = Slot(5, PlanSlotKind.Rest, "Rest", 3, optional: true);

    private static WeekPlanBuilder.CalendarWeekResult Build(
        DateOnly today,
        IEnumerable<WeekPlanBuilder.SlotInput> slots,
        Dictionary<int, WeekPlanBuilder.Placement> placements,
        IEnumerable<int>? filled = null,
        IEnumerable<WeekPlanBuilder.OverrideInput>? overrides = null) =>
        WeekPlanBuilder.BuildFromCalendar(
            Monday, today, slots.ToList(), (overrides ?? []).ToList(), (filled ?? []).ToHashSet(), placements);

    private static WeekPlanBuilder.Placement At(int dayOffset, TimeOfDay? part = TimeOfDay.Morning) =>
        new(Monday.AddDays(dayOffset), part, $"uid-{dayOffset}");

    [Fact]
    public void SessionsSitOnTheDayTheCalendarSaysAndTheRestWaitInUnplaced()
    {
        var r = Build(Monday, [Gym1, Field1, Field2], new() { [1] = At(3, TimeOfDay.Evening) });

        var placed = Assert.Single(r.Days[3].Slots);
        Assert.Equal(("Gym 1", "Evening", "uid-3"), (placed.Label, placed.TimeOfDay?.ToString(), placed.CalendarUid));
        Assert.Equal(["Field 1", "Field 2"], r.Unplaced.Select(s => s.Label));
    }

    [Fact]
    public void RoutinesAndRestAreNotSessionsSoTheyAppearNowhere()
    {
        var r = Build(Monday, [Gym1, Jump, Rest], new() { [1] = At(1) });

        Assert.Equal(1, r.Days.Sum(d => d.Slots.Count));
        Assert.Empty(r.Unplaced);
    }

    [Fact]
    public void ASessionDoneADayEarlyOrLateStillReadsAsDone()
    {
        // Planned Thursday, logged earlier in the week: filled, so Done, not Missed.
        var r = Build(Wednesday.AddDays(3), [Gym1], new() { [1] = At(3) }, filled: [1]);
        Assert.Equal(WeekPlanBuilder.SlotState.Done, Assert.Single(r.Days[3].Slots).State);
    }

    [Fact]
    public void AnUnfilledSessionIsMissedOnlyOnceItsEventsDayHasPassed()
    {
        var slots = new[] { Gym1 };
        var placements = new Dictionary<int, WeekPlanBuilder.Placement> { [1] = At(2) };

        Assert.Equal(WeekPlanBuilder.SlotState.Upcoming, Assert.Single(Build(Wednesday, slots, placements).Days[2].Slots).State);
        Assert.Equal(WeekPlanBuilder.SlotState.Missed, Assert.Single(Build(Wednesday.AddDays(1), slots, placements).Days[2].Slots).State);
    }

    [Fact]
    public void AnUnplacedSessionIsNeverMissed()
    {
        var r = Build(Monday.AddDays(6), [Gym1], []);
        Assert.Equal(WeekPlanBuilder.SlotState.Upcoming, Assert.Single(r.Unplaced).State);
    }

    [Fact]
    public void AManualSkipOrTickWinsOverTheLogs()
    {
        var skipped = Build(Monday.AddDays(6), [Gym1], new() { [1] = At(1) },
            overrides: [new(1, null, PlanSlotStatus.Skipped)]);
        Assert.Equal(WeekPlanBuilder.SlotState.Skipped, Assert.Single(skipped.Days[1].Slots).State);

        var ticked = Build(Monday.AddDays(6), [Gym1], new() { [1] = At(1) },
            overrides: [new(1, null, PlanSlotStatus.Done)]);
        var slot = Assert.Single(ticked.Days[1].Slots);
        Assert.Equal((WeekPlanBuilder.SlotState.Done, true), (slot.State, slot.IsManual));
    }

    [Fact]
    public void SessionsInADayAreOrderedMorningToEvening()
    {
        var r = Build(Monday, [Gym1, Field1], new() { [1] = At(1, TimeOfDay.Evening), [2] = At(1, TimeOfDay.Morning) });
        Assert.Equal(["Field 1", "Gym 1"], r.Days[1].Slots.Select(s => s.Label));
    }

    [Fact]
    public void SpacingWarningsRunOnTheCalendarsLayout()
    {
        // Heavy Gym 1 on Monday, Field 1 on Tuesday: the day-before rule fires.
        var r = Build(Monday, [Gym1, Field1, Field2], new() { [1] = At(0), [2] = At(1), [3] = At(4) });
        Assert.Contains(r.Warnings, w => w.Contains("Gym 1") && w.Contains("Field 1"));
    }

    [Fact]
    public void ABlankWeekHasNoWarnings()
    {
        // Nothing planned yet is not a week breaking the rules.
        Assert.Empty(Build(Monday, [Gym1, Field1], []).Warnings);
    }
}
