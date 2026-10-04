using Callahan.Api.Controllers;
using Callahan.Api.Data;
using Callahan.Api.DTOs;
using Callahan.Api.Models;
using Callahan.Api.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;

namespace Callahan.Api.Tests;

// The Today widget route and the planner page share PlanWeekLoader, so one fixture
// drives both: if the extraction had changed what the Plan page returns, or the
// widget disagreed with it, these fail. Plan slots are not seeded in the test
// database (they point at templates that live in a migration), so each test builds
// its own two-slot program: a daily-style routine slot on Monday and a rest day on
// Sunday.
public class WidgetTodayControllerTests
{
    private const string Key = "test-widget-key-0123456789";

    // Mon 28 Sep 2026; "now" is Tuesday morning unless a test says otherwise.
    private static readonly DateOnly Monday = new(2026, 9, 28);
    private static readonly DateTime TuesdayMorning = new(2026, 9, 29, 9, 0, 0);

    private static IConfiguration Config(string? key) => new ConfigurationBuilder()
        .AddInMemoryCollection(key is null ? [] : new Dictionary<string, string?> { ["Widget:Key"] = key })
        .Build();

    private static WidgetController Widget(AppDbContext db, DateTime now, string? configured, string? supplied)
    {
        var http = new DefaultHttpContext();
        if (supplied is not null) http.Request.Headers[WidgetController.KeyHeader] = supplied;
        return new WidgetController(db, Config(configured), FixedTimeProvider.Melbourne(now))
        {
            ControllerContext = new ControllerContext { HttpContext = http },
        };
    }

    private static void SeedProgram(AppDbContext db, bool mondayDone)
    {
        db.PlanSlots.AddRange(
            new PlanSlot { DefaultDayOfWeek = 0, SlotOrder = 0, Kind = PlanSlotKind.Routine, Label = "Ankle circuit", RoutineId = 1 },
            new PlanSlot { DefaultDayOfWeek = 6, SlotOrder = 0, Kind = PlanSlotKind.Rest, Label = "Rest" });
        if (mondayDone) db.RoutineCompletions.Add(new RoutineCompletion { RoutineId = 1, Date = Monday });
        db.SaveChanges();
    }

    [Fact]
    public async Task ThePlanPageStillLaysOutTheWeekFromTheSharedLoader()
    {
        using var conn = new SqliteConnection("DataSource=:memory:");
        using var db = TestData.OpenDb(conn);
        SeedProgram(db, mondayDone: true);

        var plan = new PlanController(db, FixedTimeProvider.Melbourne(TuesdayMorning));
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

    [Theory]
    [InlineData(null, null)]
    [InlineData(null, Key)]
    [InlineData(Key, null)]
    [InlineData(Key, "wrong-key")]
    public async Task AnyKeyProblemIs404(string? configured, string? supplied)
    {
        using var conn = new SqliteConnection("DataSource=:memory:");
        using var db = TestData.OpenDb(conn);
        SeedProgram(db, mondayDone: true);

        var result = await Widget(db, TuesdayMorning, configured, supplied).GetToday();

        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task TodayCountsTheWeekAndLooksToNextWeekWhenNothingIsLeft()
    {
        using var conn = new SqliteConnection("DataSource=:memory:");
        using var db = TestData.OpenDb(conn);
        SeedProgram(db, mondayDone: true);

        var result = await Widget(db, TuesdayMorning, Key, Key).GetToday();

        var dto = Assert.IsType<WidgetTodayDto>(Assert.IsType<OkObjectResult>(result.Result).Value);
        Assert.Equal(new DateOnly(2026, 9, 29), dto.TrainingDay);
        Assert.Empty(dto.Today);
        Assert.Equal(1, dto.WeekDone);
        Assert.Equal(1, dto.WeekPlanned); // the Sunday rest day doesn't count
        Assert.Equal(7, dto.Days.Count);
        Assert.Equal("Done", dto.Days[0].State);
        Assert.Equal("Rest", dto.Days[6].State);

        // Nothing left this week, so "next" is Monday of next week.
        Assert.NotNull(dto.Next);
        Assert.Equal("Ankle circuit", dto.Next.Label);
        Assert.Equal("Monday", dto.Next.DayName);
        Assert.Equal(Monday.AddDays(7), dto.Next.Date);
    }

    [Fact]
    public async Task AMissedSessionShowsAsMissedAndStillCountsAsPlanned()
    {
        using var conn = new SqliteConnection("DataSource=:memory:");
        using var db = TestData.OpenDb(conn);
        SeedProgram(db, mondayDone: false); // Monday was not logged, and it is now Tuesday

        var result = await Widget(db, TuesdayMorning, Key, Key).GetToday();

        var dto = Assert.IsType<WidgetTodayDto>(Assert.IsType<OkObjectResult>(result.Result).Value);
        Assert.Equal("Missed", dto.Days[0].State);
        Assert.Equal(0, dto.WeekDone);
        Assert.Equal(1, dto.WeekPlanned);

        // Missed is in the past, so nothing is left this week and next week's Monday is next.
        Assert.Equal(Monday.AddDays(7), dto.Next!.Date);
    }

    // Between midnight and 3 am the day still in progress is yesterday's, and the
    // widget must say so rather than flipping to a new day early.
    [Fact]
    public async Task BeforeThreeAmTodayIsStillYesterday()
    {
        using var conn = new SqliteConnection("DataSource=:memory:");
        using var db = TestData.OpenDb(conn);
        SeedProgram(db, mondayDone: true);

        var result = await Widget(db, new DateTime(2026, 9, 29, 1, 30, 0), Key, Key).GetToday();

        var dto = Assert.IsType<WidgetTodayDto>(Assert.IsType<OkObjectResult>(result.Result).Value);
        Assert.Equal(Monday, dto.TrainingDay);
        Assert.Equal("Ankle circuit", Assert.Single(dto.Today).Label);
        Assert.Equal("Done", dto.Today[0].State);
    }
}
