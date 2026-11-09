using Callahan.Api.Controllers;
using Callahan.Api.DTOs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;

namespace Callahan.Api.Tests;

// Body weight arrives as one batch per sync. What matters: a repeat sync is idempotent,
// a short window never clears older days, and a unit slip is refused rather than stored.
public class BodyWeightsControllerTests
{
    private static readonly DateOnly D1 = new(2026, 10, 24);
    private static readonly DateOnly D2 = new(2026, 11, 4);

    private static BodyWeightDto W(DateOnly d, double kg, string src) => new(d, kg, src);

    private static async Task<List<BodyWeightDto>> ReadAll(BodyWeightsController c)
    {
        var ok = Assert.IsType<OkObjectResult>((await c.GetAll()).Result);
        return Assert.IsType<List<BodyWeightDto>>(ok.Value);
    }

    private static async Task<UpsertBodyWeightsResultDto> Put(BodyWeightsController c, params BodyWeightDto[] entries)
    {
        var ok = Assert.IsType<OkObjectResult>((await c.Upsert(new UpsertBodyWeightsRequest(entries.ToList()))).Result);
        return Assert.IsType<UpsertBodyWeightsResultDto>(ok.Value);
    }

    [Fact]
    public async Task EntriesRoundTripInDateOrder()
    {
        using var conn = new SqliteConnection("DataSource=:memory:");
        using var db = TestData.OpenDb(conn);
        var c = new BodyWeightsController(db);

        var result = await Put(c, W(D2, 78.699, "MANUAL"), W(D1, 78.8, "MANUAL"));

        Assert.Equal(2, result.Created);
        var rows = await ReadAll(c);
        Assert.Equal([D1, D2], rows.Select(r => r.Date).ToArray());
        Assert.Equal(78.699, rows[1].WeightKg);
        Assert.Equal("MANUAL", rows[1].Source);
    }

    [Fact]
    public async Task ARepeatSyncChangesNothingAndACorrectedReadingUpdates()
    {
        using var conn = new SqliteConnection("DataSource=:memory:");
        using var db = TestData.OpenDb(conn);
        var c = new BodyWeightsController(db);

        await Put(c, W(D1, 78.8, "MANUAL"));
        var again = await Put(c, W(D1, 78.8, "MANUAL"));
        Assert.Equal(0, again.Created);
        Assert.Equal(0, again.Updated);

        var corrected = await Put(c, W(D1, 79.1, "MANUAL"));
        Assert.Equal(1, corrected.Updated);
        Assert.Equal(79.1, Assert.Single(await ReadAll(c)).WeightKg);
    }

    [Fact]
    public async Task AShortWindowLeavesOlderDaysAlone()
    {
        using var conn = new SqliteConnection("DataSource=:memory:");
        using var db = TestData.OpenDb(conn);
        var c = new BodyWeightsController(db);

        await Put(c, W(D1, 78.8, "MANUAL"));
        await Put(c, W(D2, 78.7, "MANUAL"));

        Assert.Equal(2, (await ReadAll(c)).Count);
    }

    [Fact]
    public async Task UnitSlipsAndFutureDatesAreSkippedNotStored()
    {
        using var conn = new SqliteConnection("DataSource=:memory:");
        using var db = TestData.OpenDb(conn);
        var c = new BodyWeightsController(db);

        var result = await Put(c,
            W(D1, 78699, "MANUAL"),                // grams sent as kilograms
            W(D2, 0, "MANUAL"),
            W(new DateOnly(2099, 1, 1), 79, "MANUAL"),
            W(D2, 78.7, "MANUAL"));

        Assert.Equal(1, result.Created);
        Assert.Equal(3, result.Skipped);
        Assert.Equal(D2, Assert.Single(await ReadAll(c)).Date);
    }

    [Fact]
    public async Task TheLastEntryForADayWins()
    {
        using var conn = new SqliteConnection("DataSource=:memory:");
        using var db = TestData.OpenDb(conn);
        var c = new BodyWeightsController(db);

        await Put(c, W(D1, 79.5, "MANUAL"), W(D1, 78.8, "INDEX_SCALE"));

        var row = Assert.Single(await ReadAll(c));
        Assert.Equal(78.8, row.WeightKg);
        Assert.Equal("INDEX_SCALE", row.Source);
    }
}
