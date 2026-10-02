using Callahan.Api.Controllers;
using Callahan.Api.DTOs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;

namespace Callahan.Api.Tests;

// The Garmin Training Status columns ride the existing wellness upsert. What
// matters: they survive a PUT -> GET round trip through the real migrated
// schema, and (as with every other wellness field) a later PUT with nulls
// clears them rather than leaving a retracted status behind.
public class WellnessTrainingStatusTests
{
    private static readonly DateOnly Day = new(2026, 10, 1);

    private static async Task<List<DailyWellnessDto>> ReadAll(WellnessController controller)
    {
        var ok = Assert.IsType<OkObjectResult>((await controller.GetAll()).Result);
        return Assert.IsType<List<DailyWellnessDto>>(ok.Value);
    }

    [Fact]
    public async Task TrainingStatusFieldsRoundTrip()
    {
        using var conn = new SqliteConnection("DataSource=:memory:");
        using var db = TestData.OpenDb(conn);
        var controller = new WellnessController(db);

        await controller.Upsert(new UpsertDailyWellnessRequest(
            Day,
            TrainingStatusCode: 3,
            TrainingStatusPhrase: "OVERREACHING_5",
            AcuteLoad: 431,
            ChronicLoad: 245,
            AcwrRatio: 1.7,
            Vo2Max: 50.5));

        var row = Assert.Single(await ReadAll(controller));
        Assert.Equal(3, row.TrainingStatusCode);
        Assert.Equal("OVERREACHING_5", row.TrainingStatusPhrase);
        Assert.Equal(431, row.AcuteLoad);
        Assert.Equal(245, row.ChronicLoad);
        Assert.Equal(1.7, row.AcwrRatio);
        Assert.Equal(50.5, row.Vo2Max);
    }

    [Fact]
    public async Task ARepeatUpsertWithoutAStatusClearsIt()
    {
        using var conn = new SqliteConnection("DataSource=:memory:");
        using var db = TestData.OpenDb(conn);
        var controller = new WellnessController(db);

        await controller.Upsert(new UpsertDailyWellnessRequest(Day, TrainingStatusCode: 7, AcwrRatio: 1.1));
        await controller.Upsert(new UpsertDailyWellnessRequest(Day, SleepScore: 80));

        var row = Assert.Single(await ReadAll(controller));
        Assert.Null(row.TrainingStatusCode);
        Assert.Null(row.AcwrRatio);
        Assert.Equal(80, row.SleepScore);
    }
}
