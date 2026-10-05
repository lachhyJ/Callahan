using Callahan.Api.Controllers;
using Callahan.Api.DTOs;
using Callahan.Api.Models;
using Callahan.Api.Services;
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
}
