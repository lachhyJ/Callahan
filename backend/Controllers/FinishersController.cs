using Callahan.Api.Data;
using Callahan.Api.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Callahan.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class FinishersController : ControllerBase
{
    private readonly AppDbContext _db;

    public FinishersController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<ActionResult<List<FinisherDto>>> GetAll()
    {
        var finishers = await _db.Finishers
            .OrderBy(f => f.SortOrder)
            .Include(f => f.Exercise)
            .ToListAsync();

        var result = new List<FinisherDto>();
        foreach (var f in finishers)
        {
            // Finishers aren't tied to one template — match "previous" by exercise, across any session.
            var lastSet = await _db.ExerciseSets
                .Where(s => s.ExerciseId == f.ExerciseId)
                .Include(s => s.WorkoutSession)
                .OrderByDescending(s => s.WorkoutSession.Date)
                .ThenByDescending(s => s.WorkoutSessionId)
                .Select(s => s.WorkoutSessionId)
                .FirstOrDefaultAsync();

            var previousSets = lastSet == 0
                ? []
                : await _db.ExerciseSets
                    .Where(s => s.WorkoutSessionId == lastSet && s.ExerciseId == f.ExerciseId)
                    .OrderBy(s => s.SetOrder)
                    .Select(s => new PreviousSetDto(s.SetOrder, s.Reps, s.WeightKg))
                    .ToListAsync();

            result.Add(new FinisherDto(f.ExerciseId, f.Exercise.Name, f.TargetSets, f.TargetReps, f.RestSeconds, previousSets));
        }

        return Ok(result);
    }
}
