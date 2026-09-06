using Callahan.Api.Models;
using Callahan.Api.Services;
using static Callahan.Api.Services.WeekPlanBuilder;

namespace Callahan.Api.Tests;

// The planner's whole job is deciding whether a planned slot happened, and
// warning when a rearranged week breaks the program's spacing. Both are pure
// functions of the week's layout and what got logged, so they are tested
// without a database or a clock.
public class WeekPlanBuilderTests
{
    private static readonly DateOnly Monday = new(2026, 9, 7); // a real Monday
    private static readonly DateOnly Wednesday = Monday.AddDays(2);

    private static SlotInput Gym(int id, int day, LowerBodyLoad load, int templateId = 4, string label = "Gym") =>
        new(id, day, 1, PlanSlotKind.Gym, label, false, templateId, null, null, load);

    private static SlotInput Field(int id, int day, int typeId = 9, string label = "Field") =>
        new(id, day, 1, PlanSlotKind.Field, label, false, null, typeId, null, null);

    private static SlotInput Routine(int id, int day, int routineId = 2) =>
        new(id, day, 1, PlanSlotKind.Routine, "Jump Block", false, null, null, routineId, null);

    private static readonly LoggedInput NothingLogged = new([], [], []);

    private static SlotResult Find(WeekResult w, int slotId) =>
        w.Days.SelectMany(d => d.Slots).Single(s => s.SlotId == slotId);

    private static int DayOf(WeekResult w, int slotId) =>
        w.Days.Single(d => d.Slots.Any(s => s.SlotId == slotId)).DayOfWeek;

    [Fact]
    public void SlotsSitOnTheirDefaultDayWhenNothingIsOverridden()
    {
        var week = Build(Monday, Monday, [Gym(1, 3, LowerBodyLoad.Heavy)], [], NothingLogged);

        Assert.Equal(3, DayOf(week, 1));
        Assert.False(Find(week, 1).IsMoved);
    }

    [Fact]
    public void AnOverrideMovesTheSlotAndFlagsItAsMoved()
    {
        var week = Build(Monday, Monday, [Gym(1, 3, LowerBodyLoad.Heavy)],
            [new OverrideInput(1, 5, PlanSlotStatus.Auto)], NothingLogged);

        Assert.Equal(5, DayOf(week, 1));
        Assert.True(Find(week, 1).IsMoved);
    }

    [Fact]
    public void MovingASlotBackToItsDefaultDayIsNotFlaggedAsMoved()
    {
        var week = Build(Monday, Monday, [Gym(1, 3, LowerBodyLoad.Heavy)],
            [new OverrideInput(1, 3, PlanSlotStatus.Auto)], NothingLogged);

        Assert.False(Find(week, 1).IsMoved);
    }

    [Fact]
    public void AGymSlotIsSatisfiedByAMatchingSessionOnItsOwnDay()
    {
        var logged = new LoggedInput([(Wednesday, 4)], [], []);
        var week = Build(Monday, Monday, [Gym(1, 2, LowerBodyLoad.Heavy, templateId: 4)], [], logged);

        Assert.Equal(SlotState.Done, Find(week, 1).State);
        Assert.False(Find(week, 1).IsManual);
    }

    [Fact]
    public void AMatchingSessionOnADifferentDayDoesNotSatisfyTheSlot()
    {
        var logged = new LoggedInput([(Monday, 4)], [], []);
        // Slot sits on Wednesday; the session was Monday.
        var week = Build(Monday, Monday.AddDays(6), [Gym(1, 2, LowerBodyLoad.Heavy, templateId: 4)], [], logged);

        Assert.Equal(SlotState.Missed, Find(week, 1).State);
    }

    [Fact]
    public void ADifferentTemplateOnTheRightDayDoesNotSatisfyTheSlot()
    {
        var logged = new LoggedInput([(Wednesday, 5)], [], []);
        var week = Build(Monday, Monday.AddDays(6), [Gym(1, 2, LowerBodyLoad.Heavy, templateId: 4)], [], logged);

        Assert.Equal(SlotState.Missed, Find(week, 1).State);
    }

    [Fact]
    public void MovingASlotMovesWhichDayCanSatisfyIt()
    {
        var logged = new LoggedInput([(Monday.AddDays(4), 4)], [], []);
        var week = Build(Monday, Monday.AddDays(6),
            [Gym(1, 2, LowerBodyLoad.Heavy, templateId: 4)],
            [new OverrideInput(1, 4, PlanSlotStatus.Auto)],
            logged);

        Assert.Equal(SlotState.Done, Find(week, 1).State);
    }

    [Fact]
    public void FieldAndRoutineSlotsMatchTheirOwnRecordTypes()
    {
        var logged = new LoggedInput([], [(Monday, 9)], [(Wednesday, 2)]);
        var week = Build(Monday, Monday.AddDays(6),
            [Field(1, 0, typeId: 9), Routine(2, 2, routineId: 2)], [], logged);

        Assert.Equal(SlotState.Done, Find(week, 1).State);
        Assert.Equal(SlotState.Done, Find(week, 2).State);
    }

    [Fact]
    public void TodayIsStillUpcomingRatherThanMissed()
    {
        // Slot is on Wednesday and today IS Wednesday - the day isn't over.
        var week = Build(Monday, Wednesday, [Gym(1, 2, LowerBodyLoad.Heavy)], [], NothingLogged);

        Assert.Equal(SlotState.Upcoming, Find(week, 1).State);
    }

    [Fact]
    public void APastUnsatisfiedSlotIsMissed()
    {
        var week = Build(Monday, Wednesday, [Gym(1, 0, LowerBodyLoad.Heavy)], [], NothingLogged);

        Assert.Equal(SlotState.Missed, Find(week, 1).State);
    }

    [Fact]
    public void AManualDoneWinsOverNoLoggedSession()
    {
        var week = Build(Monday, Monday.AddDays(6), [Gym(1, 0, LowerBodyLoad.Heavy)],
            [new OverrideInput(1, null, PlanSlotStatus.Done)], NothingLogged);

        var slot = Find(week, 1);
        Assert.Equal(SlotState.Done, slot.State);
        Assert.True(slot.IsManual);
    }

    [Fact]
    public void SkippedIsItsOwnStateNotAMiss()
    {
        var week = Build(Monday, Monday.AddDays(6), [Gym(1, 0, LowerBodyLoad.Heavy)],
            [new OverrideInput(1, null, PlanSlotStatus.Skipped)], NothingLogged);

        Assert.Equal(SlotState.Skipped, Find(week, 1).State);
    }

    [Fact]
    public void RestAndAerobicNeverReadAsMissed()
    {
        var slots = new SlotInput[]
        {
            new(1, 0, 1, PlanSlotKind.Rest, "Rest", true, null, null, null, null),
            new(2, 1, 1, PlanSlotKind.Aerobic, "Aerobic", true, null, null, null, null),
        };
        var week = Build(Monday, Monday.AddDays(6), slots, [], NothingLogged);

        Assert.Equal(SlotState.Rest, Find(week, 1).State);
        // Aerobic can't be auto-satisfied (cycling isn't synced), but it is
        // optional - it shows as outstanding, and the page treats optional
        // differently from a missed session.
        Assert.True(Find(week, 2).IsOptional);
    }

    [Fact]
    public void EveryDayOfTheWeekIsReturnedEvenWhenEmpty()
    {
        var week = Build(Monday, Monday, [Gym(1, 0, LowerBodyLoad.Heavy)], [], NothingLogged);

        Assert.Equal(7, week.Days.Count);
        Assert.Equal(Monday.AddDays(6), week.Days[6].Date);
    }

    // --- Warnings ---------------------------------------------------------

    [Fact]
    public void TheProgramsOwnWeekProducesNoWarnings()
    {
        var slots = new[]
        {
            Field(1, 0, label: "Field 1"),
            Gym(2, 1, LowerBodyLoad.Heavy, 4, "Gym 1"),
            Gym(3, 3, LowerBodyLoad.Light, 5, "Gym 2"),
            Field(4, 4, typeId: 10, label: "Field 2"),
            Gym(5, 5, LowerBodyLoad.Heavy, 6, "Gym 3"),
        };

        var week = Build(Monday, Monday, slots, [], NothingLogged);

        Assert.Empty(week.Warnings);
    }

    [Fact]
    public void AHeavyLegDayImmediatelyBeforeAFieldSessionWarns()
    {
        // Gym 1 (heavy) dragged to Thursday, directly before Friday's Field 2.
        var slots = new[] { Gym(1, 1, LowerBodyLoad.Heavy, 4, "Gym 1"), Field(2, 4, label: "Field 2") };
        var week = Build(Monday, Monday, slots, [new OverrideInput(1, 3, PlanSlotStatus.Auto)], NothingLogged);

        Assert.Contains(week.Warnings, w => w.Contains("Gym 1") && w.Contains("Field 2"));
    }

    [Fact]
    public void ALightGymDayBeforeAFieldSessionDoesNotWarn()
    {
        var slots = new[] { Gym(1, 3, LowerBodyLoad.Light, 5, "Gym 2"), Field(2, 4, label: "Field 2") };
        var week = Build(Monday, Monday, slots, [], NothingLogged);

        Assert.DoesNotContain(week.Warnings, w => w.Contains("Gym 2") && w.Contains("day before"));
    }

    [Fact]
    public void TwoHeavyLegDaysBackToBackWarn()
    {
        var slots = new[]
        {
            Gym(1, 1, LowerBodyLoad.Heavy, 4, "Gym 1"),
            Gym(2, 2, LowerBodyLoad.Heavy, 6, "Gym 3"),
            Field(3, 0, label: "Field 1"),
            Field(4, 4, typeId: 10, label: "Field 2"),
        };
        var week = Build(Monday, Monday, slots, [], NothingLogged);

        Assert.Contains(week.Warnings, w => w.Contains("back-to-back"));
    }

    [Fact]
    public void AWeekWithFewerThanTwoFieldSessionsWarns()
    {
        var week = Build(Monday, Monday, [Field(1, 0, label: "Field 1")], [], NothingLogged);

        Assert.Contains(week.Warnings, w => w.Contains("field session"));
    }

    [Fact]
    public void WarningsCountFieldSlotsWhereTheyWereMovedTo()
    {
        // Both field sessions are present, so no shortfall warning - but moving
        // Field 2 onto the day after a heavy gym day should raise the spacing one.
        var slots = new[]
        {
            Field(1, 0, label: "Field 1"),
            Field(2, 4, typeId: 10, label: "Field 2"),
            Gym(3, 1, LowerBodyLoad.Heavy, 4, "Gym 1"),
        };
        var week = Build(Monday, Monday, slots, [new OverrideInput(2, 2, PlanSlotStatus.Auto)], NothingLogged);

        // Both field sessions are present, so no shortfall warning. Matched on
        // the shortfall wording specifically: the spacing warning also contains
        // the phrase "field session", and asserting on that alone made this
        // test contradict itself.
        Assert.DoesNotContain(week.Warnings, w => w.Contains("The program asks for two"));
        Assert.Contains(week.Warnings, w => w.Contains("Gym 1") && w.Contains("Field 2"));
    }
}
