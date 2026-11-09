using Callahan.Api.Data;
using Callahan.Api.Models;
using Callahan.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Callahan.Api.Controllers;

public record TrainingModeDto(string Mode);

[ApiController]
[Authorize]
[Route("api/training-mode")]
public class TrainingModeController : ControllerBase
{
    private readonly AppDbContext _db;

    public TrainingModeController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<ActionResult<TrainingModeDto>> Get() =>
        Ok(new TrainingModeDto((await TrainingModeStore.GetAsync(_db)).ToString()));

    [HttpPut]
    public async Task<ActionResult<TrainingModeDto>> Put(TrainingModeDto request)
    {
        if (!TrainingModeStore.TryParse(request.Mode, out var mode))
        {
            return BadRequest(new { error = "Mode must be InSeason or NoClub." });
        }

        await TrainingModeStore.SetAsync(_db, mode);
        return Ok(new TrainingModeDto(mode.ToString()));
    }
}
