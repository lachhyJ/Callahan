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
            .Select(s => new WorkoutSessionSummaryDto(s.Id, s.Date, s.Notes, s.Sets.Count, s.WorkoutTemplate != null ? s.WorkoutTemplate.Name : null))
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

        var notes = await _db.ExerciseNotes
            .Where(n => n.WorkoutSessionId == id)
            .Include(n => n.Exercise)
            .Select(n => new ExerciseNoteDto(n.ExerciseId, n.Exercise.Name, n.Notes))
            .ToListAsync();

        var dto = new WorkoutSessionDetailDto(
            session.Id,
            session.Date,
            session.Notes,
            session.Sets
                .OrderBy(set => set.SetOrder)
                .Select(set => new ExerciseSetDto(set.Id, set.ExerciseId, set.Exercise.Name, set.Reps, set.WeightKg, set.SetOrder, set.SetType.ToString()))
                .ToList(),
            notes);

        return Ok(dto);
    }

    [HttpPost]
    public async Task<ActionResult<WorkoutSessionDetailDto>> Create(CreateWorkoutSessionRequest request)
    {
        if (request.Sets.Any(s => !Enum.TryParse<SetType>(s.SetType, ignoreCase: true, out _)))
        {
            return BadRequest(new { error = "Unknown set type." });
        }

        var session = new WorkoutSession
        {
            Date = request.Date,
            Notes = request.Notes,
            WorkoutTemplateId = request.WorkoutTemplateId,
            Sets = request.Sets.Select(s => new ExerciseSet
            {
                ExerciseId = s.ExerciseId,
                Reps = s.Reps,
                WeightKg = s.WeightKg,
                SetOrder = s.SetOrder,
                SetType = Enum.Parse<SetType>(s.SetType, ignoreCase: true)
            }).ToList()
        };

        _db.WorkoutSessions.Add(session);
        await _db.SaveChangesAsync();

        if (request.ExerciseNotes is { Count: > 0 })
        {
            var notes = request.ExerciseNotes
                .Where(n => !string.IsNullOrWhiteSpace(n.Notes))
                .Select(n => new ExerciseNote
                {
                    WorkoutSessionId = session.Id,
                    ExerciseId = n.ExerciseId,
                    Notes = n.Notes
                });

            _db.ExerciseNotes.AddRange(notes);
            await _db.SaveChangesAsync();
        }

        return await GetById(session.Id);
    }
}
