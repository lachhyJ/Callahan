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
public class TournamentsController : ControllerBase
{
    private readonly AppDbContext _db;

    public TournamentsController(AppDbContext db)
    {
        _db = db;
    }

    private static TournamentDto ToDto(Tournament t, int gameCount) =>
        new(t.Id, t.Name, t.StartDate, t.EndDate, gameCount);

    [HttpGet]
    public async Task<ActionResult<List<TournamentDto>>> GetAll()
    {
        var tournaments = await _db.Tournaments
            .OrderByDescending(t => t.StartDate)
            .Select(t => new TournamentDto(t.Id, t.Name, t.StartDate, t.EndDate, t.Activities.Count))
            .ToListAsync();

        return Ok(tournaments);
    }

    [HttpPost]
    public async Task<ActionResult<TournamentDto>> Create(CreateTournamentRequest request)
    {
        if (request.EndDate < request.StartDate)
        {
            return BadRequest(new { error = "EndDate can't be before StartDate." });
        }

        var tournament = new Tournament
        {
            Name = request.Name,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
        };
        _db.Tournaments.Add(tournament);
        await _db.SaveChangesAsync();

        return Ok(ToDto(tournament, 0));
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<TournamentDto>> Update(int id, UpdateTournamentRequest request)
    {
        if (request.EndDate < request.StartDate)
        {
            return BadRequest(new { error = "EndDate can't be before StartDate." });
        }

        var tournament = await _db.Tournaments.FirstOrDefaultAsync(t => t.Id == id);
        if (tournament is null) return NotFound();

        tournament.Name = request.Name;
        tournament.StartDate = request.StartDate;
        tournament.EndDate = request.EndDate;
        await _db.SaveChangesAsync();

        var gameCount = await _db.Activities.CountAsync(a => a.TournamentId == id);
        return Ok(ToDto(tournament, gameCount));
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var tournament = await _db.Tournaments.FirstOrDefaultAsync(t => t.Id == id);
        if (tournament is null) return NotFound();

        // OnDelete(DeleteBehavior.SetNull) on Activity.TournamentId detaches
        // its games rather than deleting them - a Tournament is a grouping
        // label, not the owner of the Activity rows it groups.
        _db.Tournaments.Remove(tournament);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    // Links every Ultimate activity whose Date falls in [StartDate, EndDate]
    // and isn't already attached to a (different) tournament. Deliberately
    // Ultimate-only, not Game-only - see the matching comment in
    // ActivitiesController.Create. Re-running this is safe: already-attached
    // games (to this tournament or another) are left untouched, so it can't
    // silently steal games from a tournament with overlapping dates.
    [HttpPost("{id}/attach-games")]
    public async Task<ActionResult<AttachGamesResponse>> AttachGames(int id)
    {
        var tournament = await _db.Tournaments.FirstOrDefaultAsync(t => t.Id == id);
        if (tournament is null) return NotFound();

        var candidates = await _db.Activities
            .Where(a => a.Type == ActivityType.Ultimate
                && a.TournamentId == null
                && a.Date >= tournament.StartDate
                && a.Date <= tournament.EndDate)
            .ToListAsync();

        foreach (var activity in candidates)
        {
            activity.TournamentId = tournament.Id;
        }
        await _db.SaveChangesAsync();

        return Ok(new AttachGamesResponse(candidates.Count));
    }
}
