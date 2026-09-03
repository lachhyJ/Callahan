using Callahan.Api.Controllers;
using Callahan.Api.Data;
using Callahan.Api.DTOs;
using Callahan.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Callahan.Api.Tests;

// Tournament and TaperEvent were separate entities until 2026-09-04: every
// tournament was entered twice, once on /games to group its activities and
// once on /taper to count down to it, with no link between the two records.
// They are now one row, and these tests pin the three behaviours that merge
// introduced or changed.
public class TournamentTaperMergeTests
{
    private static AppDbContext NewDb(SqliteConnection conn)
    {
        conn.Open();
        var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(conn).Options);
        db.Database.EnsureCreated();
        return db;
    }

    // The controllers return Ok(...), so the payload is on .Result, not .Value.
    private static T Unwrap<T>(ActionResult<T> result) =>
        (T)((OkObjectResult)result.Result!).Value!;

    private static Activity Game(DateOnly date, ActivityType type = ActivityType.Ultimate) =>
        new() { Date = date, Type = type, Source = ActivitySource.Manual };

    // A tournament added on the games list is a grouping label, not a taper
    // target. If TaperDays defaulted rather than staying null, every backfilled
    // past tournament would appear on the taper page as a taper that happened.
    [Fact]
    public async Task CreatingATournamentWithoutTaperDaysLeavesItOffTheTaperSurfaces()
    {
        using var conn = new SqliteConnection("Filename=:memory:");
        using var db = NewDb(conn);
        var controller = new TournamentsController(db);

        await controller.Create(new CreateTournamentRequest(
            "Regionals", new DateOnly(2026, 3, 7), new DateOnly(2026, 3, 8), null, null));

        var saved = await db.Tournaments.SingleAsync();
        Assert.Null(saved.TaperDays);
        Assert.Null(saved.PlannedReductionPercent);
        Assert.Empty(await db.Tournaments.Where(t => t.TaperDays != null).ToListAsync());
    }

    // TaperDays and PlannedReductionPercent are set together and cleared
    // together - a planned figure with no taper, or a taper with no planned
    // figure, would break the monthly report's planned-vs-actual comparison.
    [Fact]
    public async Task SettingThenClearingTaperDaysKeepsThePlannedFigureInStep()
    {
        using var conn = new SqliteConnection("Filename=:memory:");
        using var db = NewDb(conn);
        var controller = new TournamentsController(db);

        var created = Unwrap(await controller.Create(new CreateTournamentRequest(
            "Nationals", new DateOnly(2026, 4, 10), new DateOnly(2026, 4, 12), null, 10)));

        Assert.Equal(10, created.TaperDays);
        var withTaper = await db.Tournaments.SingleAsync();
        Assert.NotNull(withTaper.PlannedReductionPercent);

        await controller.Update(created.Id, new UpdateTournamentRequest(
            "Nationals", new DateOnly(2026, 4, 10), new DateOnly(2026, 4, 12), null, null));

        var cleared = await db.Tournaments.SingleAsync();
        Assert.Null(cleared.TaperDays);
        Assert.Null(cleared.PlannedReductionPercent);
    }

    // Editing a tournament's dates used to leave game membership untouched, so
    // widening the window to cover a day you'd forgotten silently picked up
    // nothing. Games outside the new window stay attached deliberately - a
    // manual assignment made on a game's detail page must survive an edit.
    [Fact]
    public async Task WideningTheDateRangeAttachesNewlyInRangeGamesWithoutDetachingOthers()
    {
        using var conn = new SqliteConnection("Filename=:memory:");
        using var db = NewDb(conn);
        var controller = new TournamentsController(db);

        var inRangeFromTheStart = Game(new DateOnly(2026, 3, 7));
        var addedByTheWidening = Game(new DateOnly(2026, 3, 9));
        var aRunOnTheSameWeekend = Game(new DateOnly(2026, 3, 9), ActivityType.Running);
        db.Activities.AddRange(inRangeFromTheStart, addedByTheWidening, aRunOnTheSameWeekend);
        await db.SaveChangesAsync();

        var created = Unwrap(await controller.Create(new CreateTournamentRequest(
            "Regionals", new DateOnly(2026, 3, 7), new DateOnly(2026, 3, 8), null, null)));
        await controller.AttachGames(created.Id);

        // A game assigned by hand from outside the window.
        var manuallyAssigned = Game(new DateOnly(2026, 3, 21));
        manuallyAssigned.TournamentId = created.Id;
        db.Activities.Add(manuallyAssigned);
        await db.SaveChangesAsync();

        await controller.Update(created.Id, new UpdateTournamentRequest(
            "Regionals", new DateOnly(2026, 3, 7), new DateOnly(2026, 3, 9), null, null));

        Assert.Equal(created.Id, (await db.Activities.FindAsync(inRangeFromTheStart.Id))!.TournamentId);
        Assert.Equal(created.Id, (await db.Activities.FindAsync(addedByTheWidening.Id))!.TournamentId);
        Assert.Equal(created.Id, (await db.Activities.FindAsync(manuallyAssigned.Id))!.TournamentId);
        Assert.Null((await db.Activities.FindAsync(aRunOnTheSameWeekend.Id))!.TournamentId);
    }

    // Deleting a tournament detaches its games (they happened regardless) but
    // cascades its taper check-ins (they mean nothing without it). The two FKs
    // deliberately differ.
    [Fact]
    public async Task DeletingATournamentDetachesGamesButCascadesTaperCheckIns()
    {
        using var conn = new SqliteConnection("Filename=:memory:");
        using var db = NewDb(conn);
        var controller = new TournamentsController(db);

        var created = Unwrap(await controller.Create(new CreateTournamentRequest(
            "Nationals", new DateOnly(2026, 4, 10), new DateOnly(2026, 4, 12), null, 10)));

        var game = Game(new DateOnly(2026, 4, 11));
        game.TournamentId = created.Id;
        db.Activities.Add(game);
        db.TaperCheckIns.Add(new TaperCheckIn
        {
            TournamentId = created.Id,
            Date = new DateOnly(2026, 4, 5),
            Energy = 3, Soreness = 3, Motivation = 3,
            CreatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        await controller.Delete(created.Id);

        Assert.Empty(await db.Tournaments.ToListAsync());
        Assert.Empty(await db.TaperCheckIns.ToListAsync());
        var survivingGame = await db.Activities.FindAsync(game.Id);
        Assert.NotNull(survivingGame);
        Assert.Null(survivingGame!.TournamentId);
    }
}
