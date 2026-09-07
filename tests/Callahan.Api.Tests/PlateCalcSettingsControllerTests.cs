using System.Text.Json;
using Callahan.Api.Controllers;
using Callahan.Api.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Callahan.Api.Tests;

// This controller is a key/value shelf for the plate calculator's device-local
// preferences. The behaviour that matters: a repeat PUT upserts rather than
// duplicating or throwing, DELETE is idempotent, GET hands values back as
// parsed JSON so the client can drop them straight into localStorage, and a key
// that isn't one of the known settings is refused so the store can't fill with
// junk.
public class PlateCalcSettingsControllerTests
{
    private static AppDbContext NewDb(SqliteConnection conn)
    {
        conn.Open();
        var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(conn).Options);
        db.Database.EnsureCreated();
        return db;
    }

    private static JsonElement Json(string raw) => JsonSerializer.Deserialize<JsonElement>(raw);

    private static Dictionary<string, JsonElement> GetAllValue(ActionResult<Dictionary<string, JsonElement>> result)
    {
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        return Assert.IsType<Dictionary<string, JsonElement>>(ok.Value);
    }

    [Fact]
    public async Task PutThenGetReturnsTheParsedValue()
    {
        using var conn = new SqliteConnection("DataSource=:memory:");
        using var db = NewDb(conn);
        var controller = new PlateCalcSettingsController(db);

        await controller.Put("customEquipment.11", Json("""{"name":"Trap bar","kg":25}"""));

        var all = GetAllValue(await controller.GetAll());
        Assert.True(all.ContainsKey("customEquipment.11"));
        Assert.Equal("Trap bar", all["customEquipment.11"].GetProperty("name").GetString());
        Assert.Equal(25, all["customEquipment.11"].GetProperty("kg").GetInt32());
    }

    [Fact]
    public async Task PutTwiceUpsertsInsteadOfDuplicating()
    {
        using var conn = new SqliteConnection("DataSource=:memory:");
        using var db = NewDb(conn);
        var controller = new PlateCalcSettingsController(db);

        await controller.Put("availablePlates.kg", Json("[25,20,15]"));
        await controller.Put("availablePlates.kg", Json("[25,20]"));

        Assert.Equal(1, await db.PlateCalcSettings.CountAsync(s => s.Key == "availablePlates.kg"));
        var all = GetAllValue(await controller.GetAll());
        Assert.Equal(2, all["availablePlates.kg"].GetArrayLength());
    }

    [Fact]
    public async Task DeleteRemovesTheRowAndIsIdempotent()
    {
        using var conn = new SqliteConnection("DataSource=:memory:");
        using var db = NewDb(conn);
        var controller = new PlateCalcSettingsController(db);

        await controller.Put("equipmentType.11", Json("\"barbell\""));
        await controller.Delete("equipmentType.11");
        await controller.Delete("equipmentType.11");

        Assert.Empty(await db.PlateCalcSettings.ToListAsync());
    }

    [Fact]
    public async Task EquipmentTypeStringValueRoundTrips()
    {
        using var conn = new SqliteConnection("DataSource=:memory:");
        using var db = NewDb(conn);
        var controller = new PlateCalcSettingsController(db);

        await controller.Put("equipmentType.11", Json("\"added\""));

        var all = GetAllValue(await controller.GetAll());
        Assert.Equal(JsonValueKind.String, all["equipmentType.11"].ValueKind);
        Assert.Equal("added", all["equipmentType.11"].GetString());
    }

    [Fact]
    public async Task AnUnknownKeyIsRejectedAndStoresNothing()
    {
        using var conn = new SqliteConnection("DataSource=:memory:");
        using var db = NewDb(conn);
        var controller = new PlateCalcSettingsController(db);

        var result = await controller.Put("callahan_token", Json("\"stolen\""));

        Assert.IsType<BadRequestObjectResult>(result);
        Assert.Empty(await db.PlateCalcSettings.ToListAsync());
    }

    [Fact]
    public async Task CustomEquipmentKeyMustHaveANumericExerciseId()
    {
        using var conn = new SqliteConnection("DataSource=:memory:");
        using var db = NewDb(conn);
        var controller = new PlateCalcSettingsController(db);

        Assert.IsType<BadRequestObjectResult>(await controller.Put("customEquipment.", Json("{}")));
        Assert.IsType<BadRequestObjectResult>(await controller.Put("customEquipment.abc", Json("{}")));
        Assert.IsType<NoContentResult>(await controller.Put("customEquipment.11", Json("""{"name":"","kg":25}""")));
    }
}
