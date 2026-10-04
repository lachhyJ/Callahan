using Callahan.Api.Models;
using Callahan.Api.Services;
using static Callahan.Api.Services.WeekPlanBuilder;

namespace Callahan.Api.Tests;

// The Today widget's rules, on hand-built weeks: what counts toward the week, which
// slot is "next", and how a day rolls up. The builder is pure, so none of this
// needs a database.
public class WidgetTodayBuilderTests
{
    // Mon 28 Sep 2026
    private static readonly DateOnly Monday = new(2026, 9, 28);

    private static SlotResult Slot(string label, SlotState state, bool optional = false,
        PlanSlotKind kind = PlanSlotKind.Gym) =>
        new(SlotId: label.GetHashCode(), label, kind, state, optional, IsMoved: false, IsManual: false);

    private static WeekResult Week(DateOnly start, params SlotResult[][] days) =>
        new(start,
            days.Select((slots, i) => new DayResult(i, start.AddDays(i), slots.ToList())).ToList(),
            []);

    // Seven days; unlisted ones are empty.
    private static WeekResult WeekWith(DateOnly start, Dictionary<int, SlotResult[]> byDay) =>
        Week(start, Enumerable.Range(0, 7).Select(i => byDay.GetValueOrDefault(i, [])).ToArray());

    [Fact]
    public void TodayListsOnlyTodaysSlotsWithTheirStates()
    {
        var week = WeekWith(Monday, new()
        {
            [0] = [Slot("Gym 1", SlotState.Done)],
            [2] = [Slot("Gym 2", SlotState.Upcoming), Slot("Ankle", SlotState.Done, kind: PlanSlotKind.Routine)],
        });

        var dto = WidgetTodayBuilder.Build(week, null, Monday.AddDays(2));

        Assert.Equal(Monday.AddDays(2), dto.TrainingDay);
        Assert.Equal(["Gym 2", "Ankle"], dto.Today.Select(s => s.Label));
        Assert.Equal(["Upcoming", "Done"], dto.Today.Select(s => s.State));
        Assert.Equal(["Gym", "Routine"], dto.Today.Select(s => s.Kind));
    }

    [Fact]
    public void AMissedSlotCountsAsPlannedButNotDone()
    {
        var week = WeekWith(Monday, new()
        {
            [0] = [Slot("Gym 1", SlotState.Done)],
            [1] = [Slot("Gym 2", SlotState.Missed)],
            [3] = [Slot("Gym 3", SlotState.Upcoming)],
        });

        var dto = WidgetTodayBuilder.Build(week, null, Monday.AddDays(2));

        Assert.Equal(1, dto.WeekDone);
        Assert.Equal(3, dto.WeekPlanned);
    }

    [Fact]
    public void OptionalRestAndSkippedSlotsDoNotCountTowardTheWeek()
    {
        var week = WeekWith(Monday, new()
        {
            [0] = [Slot("Gym 1", SlotState.Done)],
            [1] = [Slot("Bonus ride", SlotState.Done, optional: true)],
            [2] = [Slot("Rest", SlotState.Rest)],
            [3] = [Slot("Gym 2", SlotState.Skipped)],
            [4] = [Slot("Gym 3", SlotState.Upcoming)],
        });

        var dto = WidgetTodayBuilder.Build(week, null, Monday.AddDays(2));

        Assert.Equal(1, dto.WeekDone);
        Assert.Equal(2, dto.WeekPlanned); // Gym 1 and Gym 3
    }

    [Fact]
    public void NextIsTodaysOwnRemainingSessionWhenThereIsOne()
    {
        var week = WeekWith(Monday, new()
        {
            [2] = [Slot("Gym 2", SlotState.Upcoming)],
            [4] = [Slot("Gym 3", SlotState.Upcoming)],
        });

        var next = WidgetTodayBuilder.Build(week, null, Monday.AddDays(2)).Next;

        Assert.NotNull(next);
        Assert.Equal("Gym 2", next.Label);
        Assert.Equal("Wednesday", next.DayName);
        Assert.Equal(Monday.AddDays(2), next.Date);
    }

    [Fact]
    public void NextSkipsDoneAndOptionalSlotsToTheNextRealOne()
    {
        var week = WeekWith(Monday, new()
        {
            [2] = [Slot("Gym 2", SlotState.Done)],
            [3] = [Slot("Bonus ride", SlotState.Upcoming, optional: true)],
            [4] = [Slot("Gym 3", SlotState.Upcoming)],
        });

        var next = WidgetTodayBuilder.Build(week, null, Monday.AddDays(2)).Next;

        Assert.NotNull(next);
        Assert.Equal("Gym 3", next.Label);
        Assert.Equal("Friday", next.DayName);
    }

    [Fact]
    public void NextNeverPointsBackAtAnEarlierDay()
    {
        var week = WeekWith(Monday, new()
        {
            [0] = [Slot("Gym 1", SlotState.Upcoming)], // still Upcoming in the data, but it is before today
        });

        Assert.Null(WidgetTodayBuilder.Build(week, null, Monday.AddDays(3)).Next);
    }

    [Fact]
    public void WhenNothingIsLeftThisWeekNextComesFromNextWeek()
    {
        var thisWeek = WeekWith(Monday, new() { [0] = [Slot("Gym 1", SlotState.Done)] });
        var nextMonday = Monday.AddDays(7);
        var nextWeek = WeekWith(nextMonday, new() { [1] = [Slot("Gym 1", SlotState.Upcoming)] });

        var next = WidgetTodayBuilder.Build(thisWeek, nextWeek, Monday.AddDays(6)).Next;

        Assert.NotNull(next);
        Assert.Equal("Gym 1", next.Label);
        Assert.Equal(nextMonday.AddDays(1), next.Date);
        Assert.Equal("Tuesday", next.DayName);
    }

    [Fact]
    public void NextIsNullWhenThereIsNoNextWeekToLookAt()
    {
        var thisWeek = WeekWith(Monday, new() { [0] = [Slot("Gym 1", SlotState.Done)] });

        Assert.Null(WidgetTodayBuilder.Build(thisWeek, null, Monday.AddDays(6)).Next);
    }

    [Fact]
    public void ADayRollsUpToOneWord()
    {
        var week = WeekWith(Monday, new()
        {
            [0] = [Slot("Gym 1", SlotState.Done)],
            [1] = [Slot("Gym 2", SlotState.Missed), Slot("Ankle", SlotState.Done, kind: PlanSlotKind.Routine)],
            [2] = [Slot("Gym 3", SlotState.Done), Slot("Run", SlotState.Upcoming, kind: PlanSlotKind.Field)],
            [3] = [Slot("Rest", SlotState.Rest)],
            [4] = [Slot("Bonus", SlotState.Upcoming, optional: true)],
            [5] = [Slot("Gym 4", SlotState.Upcoming)],
        });

        var states = WidgetTodayBuilder.Build(week, null, Monday.AddDays(2)).Days;

        Assert.Equal(7, states.Count);
        Assert.Equal(["Done", "Missed", "Upcoming", "Rest", "Rest", "Upcoming", "Rest"], states.Select(d => d.State));
        Assert.Equal([0, 1, 2, 3, 4, 5, 6], states.Select(d => d.DayOfWeek));
    }

    [Fact]
    public void AnEmptyWeekIsAllRestAndZeroOfZero()
    {
        var dto = WidgetTodayBuilder.Build(WeekWith(Monday, []), null, Monday);

        Assert.Empty(dto.Today);
        Assert.Null(dto.Next);
        Assert.Equal(0, dto.WeekDone);
        Assert.Equal(0, dto.WeekPlanned);
        Assert.All(dto.Days, d => Assert.Equal("Rest", d.State));
    }
}
