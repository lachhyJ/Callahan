using Callahan.Api.Data;
using Callahan.Api.DTOs;
using Callahan.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Callahan.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class SeasonsController : ControllerBase
{
    private readonly AppDbContext _db;

    public SeasonsController(AppDbContext db)
    {
        _db = db;
    }

    private static SeasonDto ToDto(Season s, int tournamentCount) =>
        new(s.Id, s.Name, s.StartDate, s.EndDate, s.TargetTournamentId, s.TargetTournament?.Name, tournamentCount);

    [HttpGet]
    public async Task<ActionResult<List<SeasonDto>>> GetAll()
    {
        var seasons = await _db.Seasons
            .OrderByDescending(s => s.StartDate)
            .Select(s => new SeasonDto(
                s.Id, s.Name, s.StartDate, s.EndDate,
                s.TargetTournamentId,
                s.TargetTournament != null ? s.TargetTournament.Name : null,
                s.Tournaments.Count))
            .ToListAsync();

        return Ok(seasons);
    }

    [HttpPost]
    public async Task<ActionResult<SeasonDto>> Create(CreateSeasonRequest request)
    {
        if (request.EndDate < request.StartDate)
        {
            return BadRequest(new { error = "EndDate can't be before StartDate." });
        }
        if (request.TargetTournamentId is int targetId
            && !await _db.Tournaments.AnyAsync(t => t.Id == targetId))
        {
            return BadRequest(new { error = "TargetTournamentId doesn't match a tournament." });
        }

        var season = new Season
        {
            Name = request.Name,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            TargetTournamentId = request.TargetTournamentId,
        };
        _db.Seasons.Add(season);
        await _db.SaveChangesAsync();
        await _db.Entry(season).Reference(s => s.TargetTournament).LoadAsync();

        return Ok(ToDto(season, 0));
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<SeasonDto>> Update(int id, UpdateSeasonRequest request)
    {
        if (request.EndDate < request.StartDate)
        {
            return BadRequest(new { error = "EndDate can't be before StartDate." });
        }
        if (request.TargetTournamentId is int targetId
            && !await _db.Tournaments.AnyAsync(t => t.Id == targetId))
        {
            return BadRequest(new { error = "TargetTournamentId doesn't match a tournament." });
        }

        var season = await _db.Seasons.FirstOrDefaultAsync(s => s.Id == id);
        if (season is null) return NotFound();

        season.Name = request.Name;
        season.StartDate = request.StartDate;
        season.EndDate = request.EndDate;
        season.TargetTournamentId = request.TargetTournamentId;
        await _db.SaveChangesAsync();
        await _db.Entry(season).Reference(s => s.TargetTournament).LoadAsync();

        var tournamentCount = await _db.Tournaments.CountAsync(t => t.SeasonId == id);
        return Ok(ToDto(season, tournamentCount));
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var season = await _db.Seasons.FirstOrDefaultAsync(s => s.Id == id);
        if (season is null) return NotFound();

        // OnDelete(DeleteBehavior.SetNull) on Tournament.SeasonId detaches the
        // tournaments rather than deleting them - a Season is a grouping label.
        _db.Seasons.Remove(season);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    // Links every tournament whose [StartDate, EndDate] falls inside the season
    // and isn't already assigned to a season. Re-run-safe - already-assigned
    // tournaments (to this season or another) are left untouched. Mirrors
    // TournamentsController.AttachGames.
    [HttpPost("{id}/attach-tournaments")]
    public async Task<ActionResult<AttachTournamentsResponse>> AttachTournaments(int id)
    {
        var season = await _db.Seasons.FirstOrDefaultAsync(s => s.Id == id);
        if (season is null) return NotFound();

        var candidates = await _db.Tournaments
            .Where(t => t.SeasonId == null
                && t.StartDate >= season.StartDate
                && t.EndDate <= season.EndDate)
            .ToListAsync();

        foreach (var tournament in candidates)
        {
            tournament.SeasonId = season.Id;
        }
        await _db.SaveChangesAsync();

        return Ok(new AttachTournamentsResponse(candidates.Count));
    }
}
