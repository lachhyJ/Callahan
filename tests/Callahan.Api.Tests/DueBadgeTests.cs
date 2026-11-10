using Callahan.Api.Controllers;
using Callahan.Api.Data;
using Callahan.Api.DTOs;
using Callahan.Api.Models;
using Callahan.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using static Callahan.Api.Services.WeekSoFarBuilder;

namespace Callahan.Api.Tests;

// The picker's "Due" badge: a Gym template whose slot was planned for an earlier day this
// week and has not been logged. The rule is pure (DueTemplateIds, built on the same Fill
// the widget uses); the controller tests check it reaches the picker through the real schema.
public class DueBadgeTests
{
    private static readonly DateOnly Monday = new(2026, 9, 28);
    private static DateOnly Day(int offset) => Monday.AddDays(offset);

    private const int GymA = 7, GymB = 8;

    // The in-season program: Gym A Monday, Gym B Friday.
    private static readonly DatedGymSlot[] InSeason = [new(GymA, 0), new(GymB, 4)];

    private static GymLog Log(int day, int? template) => new(Day(day), template, template is null ? null : "x");

    private static IReadOnlySet<int> Due(int today, params GymLog[] logs) =>
        DueTemplateIds(InSeason, logs, Monday, Day(today));

    [Fact]
    public void AMissedMondaySessionIsDueFromTuesdayOn()
    {
        Assert.Equal([GymA], Due(today: 1));
        Assert.Equal([GymA], Due(today: 3));
    }

    [Fact]
    public void ASessionPlannedForTodayIsNotOverdueYet()
    {
        Assert.Empty(Due(today: 0));
        // Friday: Gym A (Mon) is overdue, Gym B (today) is not.
        Assert.Equal([GymA], Due(today: 4));
    }

    [Fact]
    public void ALoggedSessionIsNeverDue()
    {
        Assert.Empty(Due(today: 2, Log(0, GymA)));
    }

    [Fact]
    public void ALateSessionClearsTheBadge()
    {
        Assert.Equal([GymA], Due(today: 2));
        Assert.Empty(Due(today: 3, Log(2, GymA))); // logged Wednesday instead of Monday
    }

    [Fact]
    public void DoingTheOtherSessionDoesNotCoverAMissedOne()
    {
        Assert.Equal([GymA], Due(today: 5, Log(4, GymB)));
    }

    [Fact]
    public void AMissedFridaySessionIsDueOnSaturdayAndSunday()
    {
        Assert.Equal([GymB], Due(today: 5, Log(0, GymA)));
        Assert.Equal([GymB], Due(today: 6, Log(0, GymA)));
    }

    [Fact]
    public void ACustomWorkoutFillsNothing()
    {
        Assert.Equal([GymA], Due(today: 2, Log(1, null)));
    }

    [Fact]
    public void LastWeeksSessionsDoNotCountAndLastWeeksMissesDoNotCarryOver()
    {
        // Gym A logged the Sunday before this week does not fill this Monday's slot.
        Assert.Equal([GymA], Due(today: 2, new GymLog(Monday.AddDays(-1), GymA, "x")));
        // And on the Monday itself, nothing is due however much was missed last week.
        Assert.Empty(Due(today: 0));
    }

    [Fact]
    public void EveryMissedSlotIsDueNotJustTheEarliest()
    {
        DatedGymSlot[] noClub = [new(1, 0), new(2, 2), new(3, 4)];
        var due = DueTemplateIds(noClub, [Log(4, 3)], Monday, Day(5));
        Assert.Equal(new HashSet<int> { 1, 2 }, due);
    }

    [Fact]
    public void TheSameTemplateLoggedTwiceFillsOnlyOneOfTwoSlots()
    {
        DatedGymSlot[] twice = [new(GymA, 0), new(GymA, 3)];
        Assert.Equal([GymA], DueTemplateIds(twice, [Log(0, GymA)], Monday, Day(4)));
        Assert.Empty(DueTemplateIds(twice, [Log(0, GymA), Log(2, GymA)], Monday, Day(4)));
    }

    // ---- through the controller --------------------------------------------

    private static (SqliteConnection Conn, AppDbContext Db) NewDb()
    {
        var conn = new SqliteConnection("DataSource=:memory:");
        conn.Open();
        var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(conn).Options);
        db.Database.EnsureCreated();
        // Own the templates and slots: the seed's depend on the migrations.
        db.PlanSlots.RemoveRange(db.PlanSlots);
        db.WorkoutTemplateExercises.RemoveRange(db.WorkoutTemplateExercises);
        db.WorkoutSessions.RemoveRange(db.WorkoutSessions);
        db.WorkoutTemplates.RemoveRange(db.WorkoutTemplates);
        db.SaveChanges();
        return (conn, db);
    }

    private static (WorkoutTemplate A, WorkoutTemplate B, WorkoutTemplate OffSeason) SeedWeek(AppDbContext db)
    {
        var a = new WorkoutTemplate { Name = "Gym A", Subtitle = "s", SortOrder = 1, Block = TemplateBlock.InSeason };
        var b = new WorkoutTemplate { Name = "Gym B", Subtitle = "s", SortOrder = 2, Block = TemplateBlock.InSeason };
        var g1 = new WorkoutTemplate { Name = "Gym 1", Subtitle = "s", SortOrder = 1, Block = TemplateBlock.NoClub };
        db.WorkoutTemplates.AddRange(a, b, g1);
        db.SaveChanges();
        db.PlanSlots.AddRange(
            new PlanSlot { DefaultDayOfWeek = 0, SlotOrder = 1, Kind = PlanSlotKind.Gym, Label = "Gym A", WorkoutTemplateId = a.Id, Block = TemplateBlock.InSeason },
            new PlanSlot { DefaultDayOfWeek = 4, SlotOrder = 1, Kind = PlanSlotKind.Gym, Label = "Gym B", WorkoutTemplateId = b.Id, Block = TemplateBlock.InSeason },
            new PlanSlot { DefaultDayOfWeek = 0, SlotOrder = 1, Kind = PlanSlotKind.Gym, Label = "Gym 1", WorkoutTemplateId = g1.Id, Block = TemplateBlock.NoClub });
        db.SaveChanges();
        return (a, b, g1);
    }

    private static async Task<List<WorkoutTemplateSummaryDto>> Picker(AppDbContext db, DateTime now)
    {
        var result = await new WorkoutTemplatesController(db, FixedTimeProvider.Melbourne(now)).GetAll();
        return Assert.IsType<List<WorkoutTemplateSummaryDto>>(Assert.IsType<OkObjectResult>(result.Result).Value);
    }

    [Fact]
    public async Task ThePickerFlagsTheMissedSessionOfTheCurrentBlockOnly()
    {
        var (conn, db) = NewDb();
        using (conn) using (db)
        {
            SeedWeek(db);
            await TrainingModeStore.SetAsync(db, TemplateBlock.InSeason);

            var wednesday = new DateTime(2026, 9, 30, 9, 0, 0);
            var picker = await Picker(db, wednesday);

            Assert.Equal(["Gym A", "Gym B"], picker.Select(t => t.Name));
            Assert.Equal([true, false], picker.Select(t => t.Due));
        }
    }

    [Fact]
    public async Task LoggingTheSessionClearsTheFlag()
    {
        var (conn, db) = NewDb();
        using (conn) using (db)
        {
            var (a, _, _) = SeedWeek(db);
            await TrainingModeStore.SetAsync(db, TemplateBlock.InSeason);
            db.WorkoutSessions.Add(new WorkoutSession { Date = Day(2), WorkoutTemplateId = a.Id });
            db.SaveChanges();

            var picker = await Picker(db, new DateTime(2026, 9, 30, 9, 0, 0));

            Assert.All(picker, t => Assert.False(t.Due));
        }
    }

    [Fact]
    public async Task TheOffSeasonBlockHasItsOwnDueSessions()
    {
        var (conn, db) = NewDb();
        using (conn) using (db)
        {
            SeedWeek(db);
            // No-club mode is the default.
            var picker = await Picker(db, new DateTime(2026, 9, 30, 9, 0, 0));

            var gym1 = Assert.Single(picker);
            Assert.Equal("Gym 1", gym1.Name);
            Assert.True(gym1.Due);
        }
    }

    [Fact]
    public async Task TheThreeAmCutoffKeepsEarlyTuesdayOnMonday()
    {
        var (conn, db) = NewDb();
        using (conn) using (db)
        {
            SeedWeek(db);
            await TrainingModeStore.SetAsync(db, TemplateBlock.InSeason);

            // Tuesday 01:00 is still Monday's training day, so Gym A is today's, not overdue.
            var picker = await Picker(db, new DateTime(2026, 9, 29, 1, 0, 0));

            Assert.All(picker, t => Assert.False(t.Due));
        }
    }
}
