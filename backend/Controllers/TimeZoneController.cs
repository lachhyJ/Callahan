using Callahan.Api.Data;
using Callahan.Api.Models;
using Callahan.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Callahan.Api.Controllers;

// The zone "today" is computed in. Follows the user while travelling: the
// frontend compares the device zone to this and offers a switch.
[ApiController]
[Authorize]
[Route("api/timezone")]
public class TimeZoneController(AppDbContext db, UserTimeProvider time) : ControllerBase
{
    public record TimeZoneDto(string Zone, int OffsetMinutes);
    public record SetTimeZoneRequest(string? Zone);

    private TimeZoneDto Current()
    {
        var zone = time.LocalTimeZone;
        return new TimeZoneDto(zone.Id, (int)zone.GetUtcOffset(time.GetUtcNow()).TotalMinutes);
    }

    [HttpGet]
    public ActionResult<TimeZoneDto> Get() => Current();

    [HttpPut]
    public async Task<ActionResult<TimeZoneDto>> Put([FromBody] SetTimeZoneRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Zone)
            || !TimeZoneInfo.TryFindSystemTimeZoneById(request.Zone, out var zone))
        {
            return BadRequest(new { error = "Unknown time zone id." });
        }

        var row = await db.AppSettings.FirstOrDefaultAsync(s => s.Key == UserTimeProvider.SettingKey);
        if (row is null)
        {
            db.AppSettings.Add(new AppSetting { Key = UserTimeProvider.SettingKey, Value = zone.Id });
        }
        else
        {
            row.Value = zone.Id;
        }
        await db.SaveChangesAsync();

        time.SetZone(zone);
        return Current();
    }
}
