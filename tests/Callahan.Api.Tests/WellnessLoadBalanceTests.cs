using Callahan.Api.Controllers;
using Callahan.Api.DTOs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;

namespace Callahan.Api.Tests;

// Garmin Training Load Balance shares the wellness upsert and the merge-only
// status PATCH. What matters: the ten columns survive a round trip through the
// real migrated schema, and the PATCH can write a balance on its own without
// disturbing the status or any other field of the day's row.
public class WellnessLoadBalanceTests
{
    private static readonly DateOnly Day = new(2026, 10, 22);

    private static async Task<List<DailyWellnessDto>> ReadAll(WellnessController controller)
    {
        var ok = Assert.IsType<OkObjectResult>((await controller.GetAll()).Result);
        return Assert.IsType<List<DailyWellnessDto>>(ok.Value);
    }

    // The 2026-10-22 values from a real Garmin payload.
    private static PatchTrainingStatusRequest RealBalance() => new(
        Day,
        BalanceAerobicLow: 225.95972, BalanceAerobicHigh: 386.15875, BalanceAnaerobic: 439.7343,
        BalanceAerobicLowMin: 190, BalanceAerobicLowMax: 453,
        BalanceAerobicHighMin: 290, BalanceAerobicHighMax: 553,
        BalanceAnaerobicMin: 0, BalanceAnaerobicMax: 262,
        BalancePhrase: "ANAEROBIC_FOCUS");

    private static void AssertRealBalance(DailyWellnessDto row)
    {
        Assert.Equal(225.95972, row.BalanceAerobicLow);
        Assert.Equal(386.15875, row.BalanceAerobicHigh);
        Assert.Equal(439.7343, row.BalanceAnaerobic);
        Assert.Equal(190, row.BalanceAerobicLowMin);
        Assert.Equal(453, row.BalanceAerobicLowMax);
        Assert.Equal(290, row.BalanceAerobicHighMin);
        Assert.Equal(553, row.BalanceAerobicHighMax);
        Assert.Equal(0, row.BalanceAnaerobicMin);
        Assert.Equal(262, row.BalanceAnaerobicMax);
        Assert.Equal("ANAEROBIC_FOCUS", row.BalancePhrase);
    }

    [Fact]
    public async Task BalanceFieldsRoundTripThroughTheUpsert()
    {
        using var conn = new SqliteConnection("DataSource=:memory:");
        using var db = TestData.OpenDb(conn);
        var controller = new WellnessController(db);

        await controller.Upsert(new UpsertDailyWellnessRequest(
            Day,
            BalanceAerobicLow: 225.95972, BalanceAerobicHigh: 386.15875, BalanceAnaerobic: 439.7343,
            BalanceAerobicLowMin: 190, BalanceAerobicLowMax: 453,
            BalanceAerobicHighMin: 290, BalanceAerobicHighMax: 553,
            BalanceAnaerobicMin: 0, BalanceAnaerobicMax: 262,
            BalancePhrase: "ANAEROBIC_FOCUS"));

        AssertRealBalance(Assert.Single(await ReadAll(controller)));
    }

    [Fact]
    public async Task ARepeatUpsertWithoutABalanceClearsIt()
    {
        using var conn = new SqliteConnection("DataSource=:memory:");
        using var db = TestData.OpenDb(conn);
        var controller = new WellnessController(db);

        await controller.Upsert(new UpsertDailyWellnessRequest(Day, BalanceAerobicLow: 200, BalancePhrase: "X"));
        await controller.Upsert(new UpsertDailyWellnessRequest(Day, SleepScore: 80));

        var row = Assert.Single(await ReadAll(controller));
        Assert.Null(row.BalanceAerobicLow);
        Assert.Null(row.BalancePhrase);
        Assert.Equal(80, row.SleepScore);
    }

    [Fact]
    public async Task PatchWritesTheBalanceAndLeavesStatusAndEverythingElseAlone()
    {
        using var conn = new SqliteConnection("DataSource=:memory:");
        using var db = TestData.OpenDb(conn);
        var controller = new WellnessController(db);

        await controller.Upsert(new UpsertDailyWellnessRequest(
            Day, SleepScore: 80, HrvLastNightAvg: 70, TrainingStatusCode: 7, AcwrRatio: 1.1,
            RawJson: "{\"keep\":true}"));

        await controller.PatchTrainingStatus(RealBalance());

        var row = Assert.Single(await ReadAll(controller));
        AssertRealBalance(row);
        Assert.Equal(7, row.TrainingStatusCode);
        Assert.Equal(1.1, row.AcwrRatio);
        Assert.Equal(80, row.SleepScore);
        Assert.Equal(70, row.HrvLastNightAvg);
        Assert.Equal("{\"keep\":true}", db.DailyWellness.Single().RawJson);
    }

    // A patch that carries only balance fields must not be mistaken for the
    // empty no-op: the guard lists every field, so a missed one silently drops
    // a day's balance (no row, no error).
    [Fact]
    public async Task APatchCarryingOnlyABalanceFieldIsNotANoOp()
    {
        using var conn = new SqliteConnection("DataSource=:memory:");
        using var db = TestData.OpenDb(conn);
        var controller = new WellnessController(db);

        await controller.PatchTrainingStatus(new PatchTrainingStatusRequest(Day, BalancePhrase: "AEROBIC_HIGH_SHORTAGE"));
        await controller.PatchTrainingStatus(new PatchTrainingStatusRequest(new DateOnly(2026, 10, 21), BalanceAnaerobicMax: 262));

        var rows = await ReadAll(controller);
        Assert.Equal(2, rows.Count);
        Assert.Equal("AEROBIC_HIGH_SHORTAGE", rows.Single(r => r.Date == Day).BalancePhrase);
        Assert.Equal(262, rows.Single(r => r.Date == new DateOnly(2026, 10, 21)).BalanceAnaerobicMax);
    }

    [Fact]
    public async Task PatchWithNullFieldsKeepsAnExistingBalance()
    {
        using var conn = new SqliteConnection("DataSource=:memory:");
        using var db = TestData.OpenDb(conn);
        var controller = new WellnessController(db);

        await controller.PatchTrainingStatus(RealBalance());
        // A later patch carrying only the status must not clear the balance.
        await controller.PatchTrainingStatus(new PatchTrainingStatusRequest(Day, TrainingStatusCode: 7));

        var row = Assert.Single(await ReadAll(controller));
        AssertRealBalance(row);
        Assert.Equal(7, row.TrainingStatusCode);
    }
}
