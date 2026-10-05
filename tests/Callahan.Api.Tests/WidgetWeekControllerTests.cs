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

// The "this week" route end to end through the real schema: the key check, the
// Monday-Sunday boundaries, soft deletes, which slots feed the tally, and the 3 am
// day cutoff. The matching rules themselves are in WeekSoFarBuilderTests.
public class WidgetWeekControllerTests
{
    private const string Key = "test-widget-key-0123456789";

    // Mon 28 Sep 2026; "now" is Wednesday morning unless a test says otherwise.
    private static readonly DateOnly Monday = new(2026, 9, 28);
    private static readonly DateTime WednesdayMorning = new(2026, 9, 30, 9, 0, 0);

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

    private static async Task<WidgetWeekDto> Get(AppDbContext db, DateTime? now = null)
    {
        var result = await Widget(db, now ?? WednesdayMorning, Key, Key).GetWeek();
        return Assert.IsType<WidgetWeekDto>(Assert.IsType<OkObjectResult>(result.Result).Value);
    }

    // Plan slots are not seeded in the test database (they point at templates that
    // live in a migration), so each test builds a small program: Gym 1 and Field 1 as
    // the countable sessions, plus the slots that must stay out of the tally.
    private static (WorkoutTemplate Gym1, int Field1Type) SeedProgram(AppDbContext db)
    {
        var gym1 = new WorkoutTemplate { Name = "Gym 1", Subtitle = "Lower" };
        db.WorkoutTemplates.Add(gym1);
        db.SaveChanges();

        db.PlanSlots.AddRange(
            new PlanSlot { DefaultDayOfWeek = 0, SlotOrder = 1, Kind = PlanSlotKind.Field, Label = "Field 1 - Acceleration & Jumps", ActivitySessionTypeId = 9 },
            new PlanSlot { DefaultDayOfWeek = 1, SlotOrder = 1, Kind = PlanSlotKind.Routine, Label = "Jump Block", RoutineId = 1 },
            new PlanSlot { DefaultDayOfWeek = 1, SlotOrder = 2, Kind = PlanSlotKind.Gym, Label = "Gym 1", WorkoutTemplateId = gym1.Id },
            new PlanSlot { DefaultDayOfWeek = 2, SlotOrder = 1, Kind = PlanSlotKind.Rest, Label = "Rest / throws", IsOptional = true },
            new PlanSlot { DefaultDayOfWeek = 6, SlotOrder = 1, Kind = PlanSlotKind.Field, Label = "Aerobic - easy ride, or rest", IsOptional = true, ActivitySessionTypeId = 3 });
        db.SaveChanges();
        return (gym1, 9);
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
        SeedProgram(db);

        var result = await Widget(db, WednesdayMorning, configured, supplied).GetWeek();

        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task OnlyNonOptionalGymAndFieldSlotsFeedTheTally()
    {
        using var conn = new SqliteConnection("DataSource=:memory:");
        using var db = TestData.OpenDb(conn);
        SeedProgram(db);

        var w = await Get(db);

        Assert.Equal(new WidgetTallyDto(0, 1), w.Gym);
        Assert.Equal(new WidgetTallyDto(0, 1), w.Field);
        Assert.Equal(["Field 1", "Gym 1"], w.Left); // not Jump Block, rest, or the optional aerobic slot
        Assert.Equal(Monday, w.WeekStart);
        Assert.Equal(new DateOnly(2026, 9, 30), w.TrainingDay);
    }

    [Fact]
    public async Task LoggedSessionsFillTheirSlotsAndShowOnTheirDays()
    {
        using var conn = new SqliteConnection("DataSource=:memory:");
        using var db = TestData.OpenDb(conn);
        var (gym1, _) = SeedProgram(db);
        db.WorkoutSessions.Add(new WorkoutSession { Date = Monday, WorkoutTemplateId = gym1.Id });
        db.Activities.Add(new Activity { Date = Monday.AddDays(1), Type = ActivityType.Ultimate, ActivitySessionTypeId = 9 });
        db.Activities.Add(new Activity { Date = Monday.AddDays(2), Type = ActivityType.Ultimate, ActivitySessionTypeId = 6 });
        db.SaveChanges();

        var w = await Get(db);

        Assert.Equal(new WidgetTallyDto(1, 1), w.Gym);
        Assert.Equal(new WidgetTallyDto(1, 1), w.Field);
        Assert.Empty(w.Left);
        Assert.Equal("G1", Assert.Single(w.Days[0].Chips).Short);
        Assert.Equal("F1", Assert.Single(w.Days[1].Chips).Short);
        Assert.False(Assert.Single(w.Days[2].Chips).Counted); // Pod: the only Field slot was already filled
    }

    [Fact]
    public async Task LogsOutsideTheMondayToSundayWeekAreLeftOut()
    {
        using var conn = new SqliteConnection("DataSource=:memory:");
        using var db = TestData.OpenDb(conn);
        var (gym1, _) = SeedProgram(db);
        db.WorkoutSessions.Add(new WorkoutSession { Date = Monday.AddDays(-1), WorkoutTemplateId = gym1.Id });
        db.WorkoutSessions.Add(new WorkoutSession { Date = Monday.AddDays(7), WorkoutTemplateId = gym1.Id });
        db.SaveChanges();

        var w = await Get(db);

        Assert.Equal(0, w.Gym.Done);
        Assert.All(w.Days, d => Assert.Empty(d.Chips));
    }

    [Fact]
    public async Task ASessionOnTheWeeksFirstAndLastDayIsIncluded()
    {
        using var conn = new SqliteConnection("DataSource=:memory:");
        using var db = TestData.OpenDb(conn);
        db.Activities.Add(new Activity { Date = Monday, Type = ActivityType.Running });
        db.Activities.Add(new Activity { Date = Monday.AddDays(6), Type = ActivityType.Running });
        db.SaveChanges();

        var w = await Get(db);

        Assert.Equal("Run", Assert.Single(w.Days[0].Chips).Short);
        Assert.Equal("Run", Assert.Single(w.Days[6].Chips).Short);
    }

    [Fact]
    public async Task SoftDeletedSessionsAndActivitiesDoNotCount()
    {
        using var conn = new SqliteConnection("DataSource=:memory:");
        using var db = TestData.OpenDb(conn);
        var (gym1, _) = SeedProgram(db);
        db.WorkoutSessions.Add(new WorkoutSession { Date = Monday, WorkoutTemplateId = gym1.Id, DeletedAt = DateTime.UtcNow });
        db.Activities.Add(new Activity { Date = Monday, Type = ActivityType.Ultimate, ActivitySessionTypeId = 9, DeletedAt = DateTime.UtcNow });
        db.SaveChanges();

        var w = await Get(db);

        Assert.Equal(0, w.Gym.Done);
        Assert.Equal(0, w.Field.Done);
        Assert.All(w.Days, d => Assert.Empty(d.Chips));
    }

    // Between midnight and 3 am the day in progress is still yesterday's, so a
    // Monday-morning widget at 2 am is still showing the week that just ended.
    [Fact]
    public async Task BeforeThreeAmOnMondayTheWeekIsStillLastWeek()
    {
        using var conn = new SqliteConnection("DataSource=:memory:");
        using var db = TestData.OpenDb(conn);
        var (gym1, _) = SeedProgram(db);
        var sunday = Monday.AddDays(6);
        db.WorkoutSessions.Add(new WorkoutSession { Date = sunday, WorkoutTemplateId = gym1.Id });
        db.SaveChanges();

        var w = await Get(db, new DateTime(2026, 10, 5, 2, 0, 0));

        Assert.Equal(sunday, w.TrainingDay);
        Assert.Equal(Monday, w.WeekStart);
        Assert.Equal(1, w.Gym.Done);
    }

    [Fact]
    public async Task AfterThreeAmOnMondayTheWeekIsNewAndEmpty()
    {
        using var conn = new SqliteConnection("DataSource=:memory:");
        using var db = TestData.OpenDb(conn);
        var (gym1, _) = SeedProgram(db);
        db.WorkoutSessions.Add(new WorkoutSession { Date = Monday.AddDays(6), WorkoutTemplateId = gym1.Id });
        db.SaveChanges();

        var w = await Get(db, new DateTime(2026, 10, 5, 3, 30, 0));

        Assert.Equal(new DateOnly(2026, 10, 5), w.WeekStart);
        Assert.Equal(0, w.Gym.Done);
        Assert.All(w.Days, d => Assert.Empty(d.Chips));
    }
}
