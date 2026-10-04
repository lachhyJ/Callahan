using Callahan.Api.Controllers;
using Callahan.Api.DTOs;
using Callahan.Api.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;

namespace Callahan.Api.Tests;

// The widget route carries its own key check (it opts out of the fallback
// policy), so the check is the whole of its security: every failure has to be a
// 404 and a correct key has to unlock this route only.
public class WidgetControllerTests
{
    private const string Key = "test-widget-key-0123456789";
    private static readonly DateTime Now = new(2026, 10, 4, 9, 0, 0);

    private static IConfiguration Config(string? key) => new ConfigurationBuilder()
        .AddInMemoryCollection(key is null ? [] : new Dictionary<string, string?> { ["Widget:Key"] = key })
        .Build();

    private static WidgetController Controller(
        Data.AppDbContext db, string? configuredKey, string? suppliedKey)
    {
        var http = new DefaultHttpContext();
        if (suppliedKey is not null) http.Request.Headers[WidgetController.KeyHeader] = suppliedKey;
        return new WidgetController(db, Config(configuredKey), FixedTimeProvider.Melbourne(Now))
        {
            ControllerContext = new ControllerContext { HttpContext = http },
        };
    }

    private static async Task Seed(Data.AppDbContext db, DateOnly day, int? code, double? acwr = null,
        int? acute = null, int? chronic = null)
    {
        await new WellnessController(db).Upsert(new UpsertDailyWellnessRequest(
            day, TrainingStatusCode: code, AcwrRatio: acwr, AcuteLoad: acute, ChronicLoad: chronic));
    }

    [Fact]
    public async Task NoConfiguredKeyIs404EvenWithAHeader()
    {
        using var conn = new SqliteConnection("DataSource=:memory:");
        using var db = TestData.OpenDb(conn);
        await Seed(db, new DateOnly(2026, 10, 3), 7);

        var result = await Controller(db, configuredKey: null, suppliedKey: Key).GetTrainingStatus();
        Assert.IsType<NotFoundResult>(result.Result);

        result = await Controller(db, configuredKey: "", suppliedKey: "").GetTrainingStatus();
        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("wrong-key")]
    [InlineData("test-widget-key-012345678")] // a prefix of the real key
    public async Task AMissingOrWrongKeyIs404(string? supplied)
    {
        using var conn = new SqliteConnection("DataSource=:memory:");
        using var db = TestData.OpenDb(conn);
        await Seed(db, new DateOnly(2026, 10, 3), 7);

        var result = await Controller(db, Key, supplied).GetTrainingStatus();
        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task TheRightKeyReturnsTheLatestStatusWithItsLoads()
    {
        using var conn = new SqliteConnection("DataSource=:memory:");
        using var db = TestData.OpenDb(conn);
        await Seed(db, new DateOnly(2026, 10, 2), 4);
        await Seed(db, new DateOnly(2026, 10, 3), 3, acwr: 1.7, acute: 431, chronic: 245);

        var result = await Controller(db, Key, Key).GetTrainingStatus();

        var dto = Assert.IsType<WidgetTrainingStatusDto>(Assert.IsType<OkObjectResult>(result.Result).Value);
        Assert.Equal(new DateOnly(2026, 10, 3), dto.Date);
        Assert.Equal(3, dto.Code);
        Assert.Equal(1.7, dto.AcwrRatio);
        Assert.Equal(431, dto.AcuteLoad);
        Assert.Equal(245, dto.ChronicLoad);
    }

    // Garmin creates today's row before the status is computed, so the newest
    // row is often empty; the widget should show the last day that has one.
    [Fact]
    public async Task ANewerRowWithoutAStatusIsSkipped()
    {
        using var conn = new SqliteConnection("DataSource=:memory:");
        using var db = TestData.OpenDb(conn);
        await Seed(db, new DateOnly(2026, 10, 3), 7);
        await Seed(db, new DateOnly(2026, 10, 4), code: null);

        var result = await Controller(db, Key, Key).GetTrainingStatus();

        var dto = Assert.IsType<WidgetTrainingStatusDto>(Assert.IsType<OkObjectResult>(result.Result).Value);
        Assert.Equal(new DateOnly(2026, 10, 3), dto.Date);
        Assert.Equal(7, dto.Code);
    }

    [Fact]
    public async Task NothingWithinFourteenDaysIs204()
    {
        using var conn = new SqliteConnection("DataSource=:memory:");
        using var db = TestData.OpenDb(conn);
        await Seed(db, new DateOnly(2026, 9, 19), 7); // 15 days before 4 Oct

        var result = await Controller(db, Key, Key).GetTrainingStatus();

        Assert.IsType<NoContentResult>(result.Result);
    }

    [Fact]
    public async Task AStatusExactlyFourteenDaysOldIsStillReturned()
    {
        using var conn = new SqliteConnection("DataSource=:memory:");
        using var db = TestData.OpenDb(conn);
        await Seed(db, new DateOnly(2026, 9, 20), 7);

        var result = await Controller(db, Key, Key).GetTrainingStatus();

        Assert.IsType<OkObjectResult>(result.Result);
    }
}
