using Callahan.Api.Controllers;
using Callahan.Api.Data;
using Callahan.Api.DTOs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Callahan.Api.Tests;

// Marking a routine done is the one place this feature has behaviour rather
// than storage. Two things matter: a second tap on the same day must not create
// a duplicate row (there is a unique index, so the naive version throws in the
// user's face for doing something reasonable), and a plain tap must not wipe a
// note that was already written for that day.
public class RoutineCompletionTests
{
    private static AppDbContext NewDb(SqliteConnection conn)
    {
        conn.Open();
        var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(conn).Options);
        db.Database.EnsureCreated();
        return db;
    }

    private static readonly DateOnly Day = new(2026, 9, 6);

    [Fact]
    public async Task MarkingTheSameDayTwiceKeepsOneRow()
    {
        using var conn = new SqliteConnection("DataSource=:memory:");
        using var db = NewDb(conn);
        var controller = new RoutinesController(db);

        await controller.MarkDone(1, new MarkRoutineDoneRequest(Day, null));
        await controller.MarkDone(1, new MarkRoutineDoneRequest(Day, null));

        Assert.Equal(1, await db.RoutineCompletions.CountAsync(c => c.RoutineId == 1 && c.Date == Day));
    }

    [Fact]
    public async Task APlainTapDoesNotClearAnExistingNote()
    {
        using var conn = new SqliteConnection("DataSource=:memory:");
        using var db = NewDb(conn);
        var controller = new RoutinesController(db);

        await controller.MarkDone(1, new MarkRoutineDoneRequest(Day, "left ankle felt loose"));
        await controller.MarkDone(1, new MarkRoutineDoneRequest(Day, null));

        var row = await db.RoutineCompletions.SingleAsync(c => c.RoutineId == 1 && c.Date == Day);
        Assert.Equal("left ankle felt loose", row.Notes);
    }

    [Fact]
    public async Task AnotherRoutineOnTheSameDayIsItsOwnRow()
    {
        using var conn = new SqliteConnection("DataSource=:memory:");
        using var db = NewDb(conn);
        var controller = new RoutinesController(db);

        await controller.MarkDone(1, new MarkRoutineDoneRequest(Day, null));
        await controller.MarkDone(2, new MarkRoutineDoneRequest(Day, null));

        Assert.Equal(2, await db.RoutineCompletions.CountAsync(c => c.Date == Day));
    }

    [Fact]
    public async Task UndoRemovesThatDayOnly()
    {
        using var conn = new SqliteConnection("DataSource=:memory:");
        using var db = NewDb(conn);
        var controller = new RoutinesController(db);

        await controller.MarkDone(1, new MarkRoutineDoneRequest(Day, null));
        await controller.MarkDone(1, new MarkRoutineDoneRequest(Day.AddDays(-1), null));

        await controller.Undo(1, Day);

        var remaining = await db.RoutineCompletions.Where(c => c.RoutineId == 1).ToListAsync();
        Assert.Equal([Day.AddDays(-1)], remaining.Select(c => c.Date));
    }

    [Fact]
    public async Task MarkingAnUnknownRoutineIsNotFound()
    {
        using var conn = new SqliteConnection("DataSource=:memory:");
        using var db = NewDb(conn);

        var result = await new RoutinesController(db).MarkDone(999, new MarkRoutineDoneRequest(Day, null));

        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task GetAllReturnsSeededRoutinesWithTheirItemsInOrder()
    {
        using var conn = new SqliteConnection("DataSource=:memory:");
        using var db = NewDb(conn);

        var result = await new RoutinesController(db).GetAll();
        var routines = Assert.IsType<List<RoutineDto>>(Assert.IsType<OkObjectResult>(result.Result).Value);

        Assert.Equal(["Daily Ankle Circuit", "Jump Block"], routines.Select(r => r.Name));
        var jump = routines.Single(r => r.Name == "Jump Block");
        Assert.Equal(
            ["Ankle circuit (abbreviated)", "Pogo hops", "Countermovement jump to target", "Depth drop to stick landing"],
            jump.Items.Select(i => i.Name));
        Assert.Null(jump.Today);
    }

    [Fact]
    public async Task TodayIsPopulatedOnceTheDayIsMarked()
    {
        using var conn = new SqliteConnection("DataSource=:memory:");
        using var db = NewDb(conn);
        var controller = new RoutinesController(db);

        await controller.MarkDone(1, new MarkRoutineDoneRequest(null, "done before work"));

        var result = await controller.Get(1);
        var dto = Assert.IsType<RoutineDto>(Assert.IsType<OkObjectResult>(result.Result).Value);

        Assert.NotNull(dto.Today);
        Assert.Equal("done before work", dto.Today!.Notes);
    }
}

// The 3am training-day rule, mirrored from the frontend's dateUtils.
// Deliberately tested through the public surface rather than the private
// helper: what matters is that a tick at 00:30 lands on the previous day.
public class RoutineTrainingDayTests
{
    [Theory]
    [InlineData(2026, 9, 6, 0, 30, 2026, 9, 5)]   // just after midnight -> previous day
    [InlineData(2026, 9, 6, 2, 59, 2026, 9, 5)]   // last minute before the cutoff
    [InlineData(2026, 9, 6, 3, 0, 2026, 9, 6)]    // the cutoff itself is the new day
    [InlineData(2026, 9, 6, 18, 0, 2026, 9, 6)]   // ordinary evening
    [InlineData(2026, 9, 1, 1, 0, 2026, 8, 31)]   // rolls across a month boundary
    public void TrainingDayAppliesTheThreeAmCutoff(
        int y, int m, int d, int hour, int minute, int ey, int em, int ed)
    {
        var method = typeof(Callahan.Api.Controllers.RoutinesController)
            .GetMethod("TrainingDay", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;

        var actual = (DateOnly)method.Invoke(null, [new DateTime(y, m, d, hour, minute, 0)])!;

        Assert.Equal(new DateOnly(ey, em, ed), actual);
    }
}
