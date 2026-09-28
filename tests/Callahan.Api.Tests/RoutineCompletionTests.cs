using Callahan.Api.Controllers;
using Callahan.Api.Data;
using Callahan.Api.DTOs;
using Callahan.Api.Services;
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

        var result = await controller.GetAll();
        var dto = Assert.IsType<List<RoutineDto>>(Assert.IsType<OkObjectResult>(result.Result).Value)
            .Single(r => r.Id == 1);

        Assert.NotNull(dto.Today);
        Assert.Equal("done before work", dto.Today!.Notes);
    }
}

// The list reads only the latest 30 completions, not the routine's whole
// history (the daily one gains a row every day). Today's tick and the newest-
// first order must survive that bound.
public class RoutineRecentCompletionsTests
{
    [Fact]
    public async Task OnlyTheLatest30AreReturned_NewestFirst_IncludingToday()
    {
        using var conn = new SqliteConnection("DataSource=:memory:");
        conn.Open();
        using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(conn).Options);
        db.Database.EnsureCreated();
        var today = new DateOnly(2026, 9, 15);
        for (var i = 0; i < 40; i++)
            db.RoutineCompletions.Add(new Callahan.Api.Models.RoutineCompletion { RoutineId = 1, Date = today.AddDays(-i) });
        db.SaveChanges();

        var controller = new RoutinesController(db, FixedTimeProvider.Melbourne(new DateTime(2026, 9, 15, 12, 0, 0)));
        var routine = Assert.IsType<List<RoutineDto>>(Assert.IsType<OkObjectResult>((await controller.GetAll()).Result).Value)
            .Single(r => r.Id == 1);

        Assert.Equal(30, routine.Recent.Count);
        Assert.Equal(today, routine.Recent[0].Date);
        Assert.Equal(today.AddDays(-29), routine.Recent[^1].Date);
        Assert.Equal(today, routine.Today!.Date);
    }
}

// The 3am training-day rule, mirrored from the frontend's dateUtils: an
// undated tap at 00:30 lands on the previous day. Driven through MarkDone on a
// pinned clock, so it tests the public behaviour rather than a private helper.
public class RoutineTrainingDayTests
{
    [Theory]
    [InlineData(2026, 9, 6, 0, 30, 2026, 9, 5)]   // just after midnight -> previous day
    [InlineData(2026, 9, 6, 2, 59, 2026, 9, 5)]   // last minute before the cutoff
    [InlineData(2026, 9, 6, 3, 0, 2026, 9, 6)]    // the cutoff itself is the new day
    [InlineData(2026, 9, 6, 18, 0, 2026, 9, 6)]   // ordinary evening
    [InlineData(2026, 9, 1, 1, 0, 2026, 8, 31)]   // rolls across a month boundary
    public async Task AnUndatedTapUsesTheThreeAmCutoff(
        int y, int m, int d, int hour, int minute, int ey, int em, int ed)
    {
        using var conn = new SqliteConnection("DataSource=:memory:");
        conn.Open();
        using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(conn).Options);
        db.Database.EnsureCreated();
        var clock = FixedTimeProvider.Melbourne(new DateTime(y, m, d, hour, minute, 0));

        var result = await new RoutinesController(db, clock).MarkDone(1, new MarkRoutineDoneRequest(null, null));
        var dto = Assert.IsType<RoutineCompletionDto>(Assert.IsType<OkObjectResult>(result.Result).Value);

        Assert.Equal(new DateOnly(ey, em, ed), dto.Date);
    }
}
