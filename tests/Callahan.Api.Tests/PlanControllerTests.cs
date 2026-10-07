using Callahan.Api.Controllers;
using Callahan.Api.DTOs;
using Callahan.Api.Models;
using Callahan.Api.Services;
using Callahan.Api.Services.Calendar;
using Callahan.Api.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;

namespace Callahan.Api.Tests;

// The Plan page is built from PlanWeekLoader, which was extracted from the
// controller. Nothing covered the endpoint itself, so this pins what it returns:
// the week laid out Monday-first, slot states from what was logged, and the ankle
// circuit's completed dates.
public class PlanControllerTests
{
    private static readonly DateOnly Monday = new(2026, 9, 28);

    [Fact]
    public async Task TheWeekIsLaidOutFromTheSharedLoader()
    {
        using var conn = new SqliteConnection("DataSource=:memory:");
        using var db = TestData.OpenDb(conn);
        db.PlanSlots.AddRange(
            new PlanSlot { DefaultDayOfWeek = 0, SlotOrder = 0, Kind = PlanSlotKind.Routine, Label = "Ankle circuit", RoutineId = 1 },
            new PlanSlot { DefaultDayOfWeek = 6, SlotOrder = 0, Kind = PlanSlotKind.Rest, Label = "Rest" });
        db.RoutineCompletions.Add(new RoutineCompletion { RoutineId = 1, Date = Monday });
        db.SaveChanges();

        var plan = new PlanController(db, FixedTimeProvider.Melbourne(new DateTime(2026, 9, 29, 9, 0, 0)));
        var result = await plan.Get(weekStart: Monday.AddDays(2)); // a Wednesday: snaps to its Monday

        var week = Assert.IsType<WeekPlanDto>(Assert.IsType<OkObjectResult>(result.Result).Value);
        Assert.Equal(Monday, week.WeekStart);
        Assert.Equal(7, week.Days.Count);
        Assert.Equal("Done", Assert.Single(week.Days[0].Slots).State);
        Assert.Equal("Rest", Assert.Single(week.Days[6].Slots).State);
        Assert.Equal("Monday", week.Days[0].DayName);
        Assert.Equal([Monday], week.AnkleCircuit!.CompletedDates);
        Assert.Equal(PlanWeekLoader.AnkleCircuitRoutineId, week.AnkleCircuit.RoutineId);
    }

    // --- calendar mode -----------------------------------------------------

    private sealed class FakeCalendar(IReadOnlyList<CalendarEvent>? events = null, string? down = null, bool configured = true) : ICalendarClient
    {
        public bool IsConfigured => configured;

        public Task<IReadOnlyList<CalendarEvent>> GetEventsAsync(DateOnly from, DateOnly to, CancellationToken ct) =>
            down is not null ? throw new CalendarUnavailableException(down) : Task.FromResult(events ?? []);
    }

    private static CalendarEvent Ev(string uid, string title, int dayOffset, TimeOfDay? part = TimeOfDay.Morning) =>
        new(uid, title, Monday.AddDays(dayOffset), part);

    // Gym 1 (template-backed), Field 1, the optional aerobic slot, and the slots that
    // must never get an event: a routine and a rest marker.
    private static (int Gym1, int Field1, int Aerobic, WorkoutTemplate Template) SeedSessions(AppDbContext db)
    {
        var template = new WorkoutTemplate { Name = "Gym 1", Subtitle = "Lower" };
        db.WorkoutTemplates.Add(template);
        db.SaveChanges();
        var gym = new PlanSlot { DefaultDayOfWeek = 1, SlotOrder = 2, Kind = PlanSlotKind.Gym, Label = "Gym 1", WorkoutTemplateId = template.Id };
        var field = new PlanSlot { DefaultDayOfWeek = 2, SlotOrder = 1, Kind = PlanSlotKind.Field, Label = "Field 1 - Acceleration & Jumps", ActivitySessionTypeId = 9 };
        var aerobic = new PlanSlot { DefaultDayOfWeek = 6, SlotOrder = 1, Kind = PlanSlotKind.Aerobic, Label = "Aerobic - easy ride, or rest", IsOptional = true };
        db.PlanSlots.AddRange(gym, field, aerobic,
            new PlanSlot { DefaultDayOfWeek = 1, SlotOrder = 1, Kind = PlanSlotKind.Routine, Label = "Jump Block", RoutineId = 2 },
            new PlanSlot { DefaultDayOfWeek = 3, SlotOrder = 1, Kind = PlanSlotKind.Rest, Label = "Rest", IsOptional = true });
        db.SaveChanges();
        return (gym.Id, field.Id, aerobic.Id, template);
    }

    private static PlanController Controller(AppDbContext db, ICalendarClient? calendar, DateTime? now = null) =>
        new(db, FixedTimeProvider.Melbourne(now ?? new DateTime(2026, 9, 30, 9, 0, 0)), calendar);

    private static async Task<WeekPlanDto> Week(PlanController c)
    {
        var result = await c.Get(weekStart: Monday);
        return Assert.IsType<WeekPlanDto>(Assert.IsType<OkObjectResult>(result.Result).Value);
    }

    [Fact]
    public async Task WithACalendarTheDaysComeFromItsEventsAndTheRestWaitInUnplaced()
    {
        using var conn = new SqliteConnection("DataSource=:memory:");
        using var db = TestData.OpenDb(conn);
        SeedSessions(db);
        var calendar = new FakeCalendar([Ev("g", "Gym 1", 3, TimeOfDay.Evening), Ev("d", "Dinner", 3)]);

        var week = await Week(Controller(db, calendar));

        Assert.True(week.CalendarEnabled);
        Assert.False(week.CalendarUnavailable);
        var gym = Assert.Single(week.Days[3].Slots);
        Assert.Equal(("Gym 1", "Evening", "g"), (gym.Label, gym.TimeOfDay, gym.CalendarUid));
        Assert.False(gym.IsLinked); // matched by its title
        Assert.Equal(["Field 1 - Acceleration & Jumps", "Aerobic - easy ride, or rest"], week.Unplaced!.Select(s => s.Label));
        Assert.Equal(["Dinner"], week.OtherEvents!.Select(e => e.Title));
    }

    [Fact]
    public async Task EventsOutsideTheWeekAreIgnored()
    {
        using var conn = new SqliteConnection("DataSource=:memory:");
        using var db = TestData.OpenDb(conn);
        SeedSessions(db);
        // The client may return a day either side; neither belongs to this week.
        var calendar = new FakeCalendar([Ev("before", "Gym 1", -1), Ev("after", "Field 1", 7)]);

        var week = await Week(Controller(db, calendar));

        Assert.All(week.Days, d => Assert.Empty(d.Slots));
        Assert.Empty(week.OtherEvents!);
        // Matching them anyway would take the sessions off the page entirely.
        Assert.Equal(["Gym 1", "Field 1 - Acceleration & Jumps"], week.Unplaced!.Select(s => s.Label).Take(2));
    }

    [Fact]
    public async Task ASessionLoggedAnyDayOfTheWeekIsDoneNotMissed()
    {
        using var conn = new SqliteConnection("DataSource=:memory:");
        using var db = TestData.OpenDb(conn);
        var (_, _, _, template) = SeedSessions(db);
        // Planned for Tuesday, logged on Monday; "now" is Friday so Tuesday has passed.
        db.WorkoutSessions.Add(new WorkoutSession { Date = Monday, WorkoutTemplateId = template.Id });
        db.SaveChanges();
        var calendar = new FakeCalendar([Ev("g", "Gym 1", 1)]);

        var week = await Week(Controller(db, calendar, new DateTime(2026, 10, 2, 9, 0, 0)));

        Assert.Equal("Done", Assert.Single(week.Days[1].Slots).State);
    }

    [Fact]
    public async Task APlannedSessionNothingWasLoggedForIsMissedOnceItsDayHasPassed()
    {
        using var conn = new SqliteConnection("DataSource=:memory:");
        using var db = TestData.OpenDb(conn);
        SeedSessions(db);
        var calendar = new FakeCalendar([Ev("g", "Gym 1", 1)]);

        var week = await Week(Controller(db, calendar, new DateTime(2026, 10, 2, 9, 0, 0)));

        Assert.Equal("Missed", Assert.Single(week.Days[1].Slots).State);
    }

    [Fact]
    public async Task AnUnreachableCalendarFallsBackToTheProgramsWeekWithAMessage()
    {
        using var conn = new SqliteConnection("DataSource=:memory:");
        using var db = TestData.OpenDb(conn);
        SeedSessions(db);

        var week = await Week(Controller(db, new FakeCalendar(down: "Couldn't reach your calendar.")));

        Assert.True(week.CalendarEnabled);
        Assert.True(week.CalendarUnavailable);
        Assert.Equal("Couldn't reach your calendar.", week.CalendarMessage);
        Assert.Equal("Gym 1", Assert.Single(week.Days[1].Slots.Where(s => s.Kind == "Gym")).Label); // the default day, as before
        Assert.Null(week.Unplaced);
    }

    [Fact]
    public async Task WithoutACalendarConfiguredThePageIsAsItWasBefore()
    {
        using var conn = new SqliteConnection("DataSource=:memory:");
        using var db = TestData.OpenDb(conn);
        SeedSessions(db);

        foreach (var calendar in new ICalendarClient?[] { null, new FakeCalendar(configured: false) })
        {
            var week = await Week(Controller(db, calendar));
            Assert.False(week.CalendarEnabled);
            Assert.False(week.CalendarUnavailable);
            Assert.Null(week.Unplaced);
            Assert.Equal(["Gym 1"], week.Days[1].Slots.Where(s => s.Kind == "Gym").Select(s => s.Label));
        }
    }

    [Fact]
    public async Task LinkingAnEventPlacesItsSessionAndUnlinkingTakesItBack()
    {
        using var conn = new SqliteConnection("DataSource=:memory:");
        using var db = TestData.OpenDb(conn);
        var (_, field1, _, _) = SeedSessions(db);
        var calendar = new FakeCalendar([Ev("odd", "Sprints", 4, TimeOfDay.Arvo)]);
        var controller = Controller(db, calendar);

        Assert.IsType<NoContentResult>(await controller.LinkSlot(field1, new LinkPlanSlotRequest(Monday, "odd")));
        var linked = await Week(controller);
        Assert.Equal(("Field 1 - Acceleration & Jumps", "Arvo"), (linked.Days[4].Slots.Single().Label, linked.Days[4].Slots.Single().TimeOfDay));
        Assert.True(linked.Days[4].Slots.Single().IsLinked); // tied by hand, so it can be unlinked
        Assert.Empty(linked.OtherEvents!);

        Assert.IsType<NoContentResult>(await controller.LinkSlot(field1, new LinkPlanSlotRequest(Monday, null)));
        var unlinked = await Week(controller);
        Assert.Contains(unlinked.OtherEvents!, e => e.Uid == "odd");
        Assert.Empty(db.PlanSlotWeeks); // an unlink leaves no row behind
    }

    [Fact]
    public async Task LinkingRefusesAnEventThatIsNotInThatWeekOrASlotThatIsNotASession()
    {
        using var conn = new SqliteConnection("DataSource=:memory:");
        using var db = TestData.OpenDb(conn);
        var (gym, _, _, _) = SeedSessions(db);
        var rest = db.PlanSlots.Single(s => s.Kind == PlanSlotKind.Rest).Id;
        var controller = Controller(db, new FakeCalendar([Ev("here", "Sprints", 1)]));

        Assert.IsType<BadRequestObjectResult>(await controller.LinkSlot(gym, new LinkPlanSlotRequest(Monday, "nope")));
        Assert.IsType<BadRequestObjectResult>(await controller.LinkSlot(rest, new LinkPlanSlotRequest(Monday, "here")));
        Assert.IsType<NotFoundResult>(await controller.LinkSlot(9999, new LinkPlanSlotRequest(Monday, "here")));
        Assert.Empty(db.PlanSlotWeeks);
    }

    [Fact]
    public async Task OneEventStandsForOneSessionRelinkingMovesIt()
    {
        using var conn = new SqliteConnection("DataSource=:memory:");
        using var db = TestData.OpenDb(conn);
        var (gym, field1, _, _) = SeedSessions(db);
        var controller = Controller(db, new FakeCalendar([Ev("odd", "Sprints", 4)]));

        await controller.LinkSlot(gym, new LinkPlanSlotRequest(Monday, "odd"));
        await controller.LinkSlot(field1, new LinkPlanSlotRequest(Monday, "odd"));

        var week = await Week(controller);
        Assert.Equal("Field 1 - Acceleration & Jumps", Assert.Single(week.Days[4].Slots).Label);
        Assert.Contains(week.Unplaced!, s => s.Label == "Gym 1");
    }

    [Fact]
    public async Task TickingALinkedSlotKeepsItsLinkWhenTheTickIsUndone()
    {
        using var conn = new SqliteConnection("DataSource=:memory:");
        using var db = TestData.OpenDb(conn);
        var (gym, _, _, _) = SeedSessions(db);
        var controller = Controller(db, new FakeCalendar([Ev("odd", "Sprints", 1)]));
        await controller.LinkSlot(gym, new LinkPlanSlotRequest(Monday, "odd"));

        await controller.UpdateSlot(gym, new UpdatePlanSlotRequest(Monday, null, "Done"));
        await controller.UpdateSlot(gym, new UpdatePlanSlotRequest(Monday, null, "Auto"));

        Assert.Equal("odd", db.PlanSlotWeeks.Single().CalendarUid);
    }

    [Fact]
    public async Task AFieldEventTypedAsJustFieldOneLandsOnTheSubtitledSlot()
    {
        using var conn = new SqliteConnection("DataSource=:memory:");
        using var db = TestData.OpenDb(conn);
        SeedSessions(db);

        var week = await Week(Controller(db, new FakeCalendar([Ev("f", "Field 1", 3)])));

        Assert.Equal("Field 1 - Acceleration & Jumps", Assert.Single(week.Days[3].Slots).Label);
        Assert.Empty(week.OtherEvents!);
    }
}
