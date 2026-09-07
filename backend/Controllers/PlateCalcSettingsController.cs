using System.Text.Json;
using System.Text.RegularExpressions;
using Callahan.Api.Data;
using Callahan.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Callahan.Api.Controllers;

// Server-side home for the plate calculator's device-local preferences, so they
// survive localStorage eviction and sync across devices. The frontend keeps
// localStorage as a cache and rehydrates it from here on load; every value is
// opaque to the server (see PlateCalcSetting).
[ApiController]
[Authorize]
[Route("api/[controller]")]
public partial class PlateCalcSettingsController : ControllerBase
{
    private readonly AppDbContext _db;

    public PlateCalcSettingsController(AppDbContext db)
    {
        _db = db;
    }

    // The only keys the frontend actually writes. Anything else is a bug or a
    // probe, not something to persist — an unbounded string-keyed store that
    // takes any key is an obvious junk magnet.
    [GeneratedRegex(@"^(availablePlates\.(kg|lb)|availableDumbbells\.kg|customEquipment\.\d+|equipmentType\.\d+)$")]
    private static partial Regex AllowedKey();

    [HttpGet]
    public async Task<ActionResult<Dictionary<string, JsonElement>>> GetAll()
    {
        var rows = await _db.PlateCalcSettings.AsNoTracking().ToListAsync();
        var result = new Dictionary<string, JsonElement>(rows.Count);
        foreach (var row in rows)
        {
            try
            {
                result[row.Key] = JsonSerializer.Deserialize<JsonElement>(row.ValueJson);
            }
            catch (JsonException)
            {
                // A row we can't parse is dead weight — skip it rather than 500
                // the whole hydrate.
            }
        }
        return Ok(result);
    }

    [HttpPut("{key}")]
    public async Task<IActionResult> Put(string key, [FromBody] JsonElement value)
    {
        if (!AllowedKey().IsMatch(key))
        {
            return BadRequest(new { error = $"Not a plate-calc setting key: {key}" });
        }

        var json = value.GetRawText();
        var existing = await _db.PlateCalcSettings.FindAsync(key);
        if (existing is null)
        {
            _db.PlateCalcSettings.Add(new PlateCalcSetting { Key = key, ValueJson = json });
        }
        else
        {
            existing.ValueJson = json;
        }
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{key}")]
    public async Task<IActionResult> Delete(string key)
    {
        var existing = await _db.PlateCalcSettings.FindAsync(key);
        if (existing is not null)
        {
            _db.PlateCalcSettings.Remove(existing);
            await _db.SaveChangesAsync();
        }
        return NoContent();
    }
}
