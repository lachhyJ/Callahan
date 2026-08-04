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
public class RunningSessionsController : ControllerBase
{
    private readonly AppDbContext _db;

    public RunningSessionsController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<ActionResult<List<RunningSessionDto>>> GetAll()
    {
        var sessions = await _db.RunningSessions
            .OrderByDescending(s => s.Date)
            .Select(s => new RunningSessionDto(s.Id, s.Date, s.DistanceKm, s.DurationSeconds, s.Notes))
            .ToListAsync();

        return Ok(sessions);
    }

    [HttpPost]
    public async Task<ActionResult<RunningSessionDto>> Create(CreateRunningSessionRequest request)
    {
        var session = new RunningSession
        {
            Date = request.Date,
            DistanceKm = request.DistanceKm,
            DurationSeconds = request.DurationSeconds,
            Notes = request.Notes
        };

        _db.RunningSessions.Add(session);
        await _db.SaveChangesAsync();

        return Ok(new RunningSessionDto(session.Id, session.Date, session.DistanceKm, session.DurationSeconds, session.Notes));
    }
}
