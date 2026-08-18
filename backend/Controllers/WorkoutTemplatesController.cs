using Callahan.Api.Data;
using Callahan.Api.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Callahan.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class WorkoutTemplatesController : ControllerBase
{
    private readonly AppDbContext _db;

    public WorkoutTemplatesController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<ActionResult<List<WorkoutTemplateSummaryDto>>> GetAll()
    {
        var templates = await _db.WorkoutTemplates
            .OrderBy(t => t.SortOrder)
            .Select(t => new WorkoutTemplateSummaryDto(t.Id, t.Name, t.Subtitle))
            .ToListAsync();

        return Ok(templates);
    }

    [HttpGet("{id}/start")]
    public async Task<ActionResult<WorkoutTemplateStartDto>> Start(int id)
    {
        var template = await _db.WorkoutTemplates
            .Include(t => t.Exercises).ThenInclude(te => te.Exercise).ThenInclude(e => e.MuscleTargets)
            .FirstOrDefaultAsync(t => t.Id == id);

        if (template is null) return NotFound();

        var lastSession = await _db.WorkoutSessions
            .Where(s => s.WorkoutTemplateId == id)
            .Include(s => s.Sets)
            .OrderByDescending(s => s.Date)
            .ThenByDescending(s => s.Id)
            .FirstOrDefaultAsync();

        var exercises = template.Exercises
            .OrderBy(te => te.ExerciseOrder)
            .Select(te =>
            {
                var previousSets = lastSession?.Sets
                    .Where(s => s.ExerciseId == te.ExerciseId)
                    .OrderBy(s => s.SetOrder)
                    .Select(s => new PreviousSetDto(s.SetOrder, s.Reps, s.WeightKg, s.SetType.ToString()))
                    .ToList() ?? [];

                var primaryMuscle = te.Exercise.MuscleTargets.Where(mt => mt.IsPrimary).Select(mt => mt.MuscleGroup.ToString()).FirstOrDefault();

                return new WorkoutTemplateExerciseStartDto(
                    te.Id, te.ExerciseId, te.Exercise.Name, te.TargetSets, te.TargetReps, te.RestSeconds, te.Tempo, te.Cue, primaryMuscle, te.Exercise.IsAssisted, previousSets);
            })
            .ToList();

        return Ok(new WorkoutTemplateStartDto(template.Id, template.Name, template.Subtitle, exercises));
    }

    [HttpPut("exercises/{workoutTemplateExerciseId}/cue")]
    public async Task<IActionResult> UpdateCue(int workoutTemplateExerciseId, UpdateCueRequest request)
    {
        var te = await _db.WorkoutTemplateExercises.FindAsync(workoutTemplateExerciseId);
        if (te is null) return NotFound();

        te.Cue = string.IsNullOrWhiteSpace(request.Cue) ? null : request.Cue.Trim();
        await _db.SaveChangesAsync();

        return NoContent();
    }
}
