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
public class WorkoutSessionsController : ControllerBase
{
    private readonly AppDbContext _db;

    public WorkoutSessionsController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<ActionResult<List<WorkoutSessionSummaryDto>>> GetAll()
    {
        var sessions = await _db.WorkoutSessions
            .OrderByDescending(s => s.Date)
            .Select(s => new WorkoutSessionSummaryDto(s.Id, s.Date, s.Notes, s.Sets.Count))
            .ToListAsync();

        return Ok(sessions);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<WorkoutSessionDetailDto>> GetById(int id)
    {
        var session = await _db.WorkoutSessions
            .Include(s => s.Sets).ThenInclude(set => set.Exercise)
            .FirstOrDefaultAsync(s => s.Id == id);

        if (session is null) return NotFound();

        var dto = new WorkoutSessionDetailDto(
            session.Id,
            session.Date,
            session.Notes,
            session.Sets
                .OrderBy(set => set.SetOrder)
                .Select(set => new ExerciseSetDto(set.Id, set.ExerciseId, set.Exercise.Name, set.Reps, set.WeightKg, set.SetOrder))
                .ToList());

        return Ok(dto);
    }

    [HttpPost]
    public async Task<ActionResult<WorkoutSessionDetailDto>> Create(CreateWorkoutSessionRequest request)
    {
        var session = new WorkoutSession
        {
            Date = request.Date,
            Notes = request.Notes,
            Sets = request.Sets.Select(s => new ExerciseSet
            {
                ExerciseId = s.ExerciseId,
                Reps = s.Reps,
                WeightKg = s.WeightKg,
                SetOrder = s.SetOrder
            }).ToList()
        };

        _db.WorkoutSessions.Add(session);
        await _db.SaveChangesAsync();

        return await GetById(session.Id);
    }
}
