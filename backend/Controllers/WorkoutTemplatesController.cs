using Callahan.Api.Data;
using Callahan.Api.DTOs;
using Callahan.Api.Services;
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
        // Retired templates are hidden here and nowhere else - Start() below
        // still serves them, so an old session's template can always be
        // reopened by Id.
        var templates = await _db.WorkoutTemplates
            .Where(t => !t.IsRetired)
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
                    .Select(s => new PreviousSetDto(s.SetOrder, s.Reps, s.WeightKg, s.SetType.ToString(), s.DurationSeconds))
                    .ToList() ?? [];

                var primaryMuscle = te.Exercise.MuscleTargets.Where(mt => mt.IsPrimary).Select(mt => mt.MuscleGroup.ToString()).FirstOrDefault();

                var readiness = ProgressionReadinessChecker.Evaluate(
                    new ProgressionReadinessChecker.SlotInput(te.TargetSets, te.TargetRepsMax),
                    lastSession?.Date,
                    lastSession?.Sets
                        .Where(s => s.ExerciseId == te.ExerciseId)
                        .Select(s => new ProgressionReadinessChecker.SetInput(s.SetOrder, s.Reps, s.SetType))
                        .ToList() ?? []);

                return new WorkoutTemplateExerciseStartDto(
                    te.Id, te.ExerciseId, te.Exercise.Name, te.TargetSets, te.WarmupSets, te.TargetReps, te.RestSeconds, te.Tempo, te.Cue, primaryMuscle,
                    te.Exercise.IsAssisted, te.Exercise.IsTimeBased, te.Exercise.IsPerSide, te.Exercise.PerSideDelaySeconds, te.TargetDurationSeconds, te.SupersetWithNext, previousSets,
                    readiness.Ready);
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

    // Persists the athlete's chosen rest duration for this program slot, so
    // it follows them into future sessions — distinct from a live rest
    // timer's own in-the-moment +15/-15 nudges, which only ever adjust the
    // running countdown and never reach this endpoint.
    [HttpPut("exercises/{workoutTemplateExerciseId}/rest-seconds")]
    public async Task<IActionResult> UpdateRestSeconds(int workoutTemplateExerciseId, UpdateRestSecondsRequest request)
    {
        var te = await _db.WorkoutTemplateExercises.FindAsync(workoutTemplateExerciseId);
        if (te is null) return NotFound();
        if (request.RestSeconds < 0) return BadRequest();

        te.RestSeconds = request.RestSeconds;
        await _db.SaveChangesAsync();

        return NoContent();
    }

    // Rewrites the slot order and superset links for a whole template, as the
    // athlete arranged them in the active workout's Rearrange mode. Same
    // "session edit follows you into future sessions" idea as UpdateRestSeconds
    // and UpdateCue, but it touches every slot at once so it takes the template
    // id and the full list rather than one slot.
    [HttpPut("{templateId}/layout")]
    public async Task<IActionResult> UpdateLayout(int templateId, UpdateTemplateLayoutRequest request)
    {
        var slots = await _db.WorkoutTemplateExercises
            .Where(te => te.WorkoutTemplateId == templateId)
            .ToListAsync();
        if (slots.Count == 0) return NotFound();

        var byId = slots.ToDictionary(s => s.Id);

        // The client sends every template slot exactly once. A payload that
        // references a slot from another template, or doesn't cover them all, is
        // a bug on the sending side — reject the whole thing rather than apply a
        // partial reorder.
        var ids = request.Items.Select(i => i.WorkoutTemplateExerciseId).ToList();
        if (ids.Count != slots.Count || ids.Distinct().Count() != ids.Count
            || ids.Any(id => !byId.ContainsKey(id)))
        {
            return BadRequest(new { error = "Layout must list every slot of this template exactly once." });
        }

        foreach (var item in request.Items)
        {
            var slot = byId[item.WorkoutTemplateExerciseId];
            slot.ExerciseOrder = item.ExerciseOrder;
            slot.SupersetWithNext = item.SupersetWithNext;
        }

        // A "link to next" on the last slot by order is meaningless — there is
        // no next. Sanitise server-side whatever the client sent.
        var last = request.Items
            .OrderByDescending(i => i.ExerciseOrder)
            .Select(i => byId[i.WorkoutTemplateExerciseId])
            .First();
        last.SupersetWithNext = false;

        await _db.SaveChangesAsync();

        return NoContent();
    }
}
