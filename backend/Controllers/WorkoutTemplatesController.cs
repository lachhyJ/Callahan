using Callahan.Api.Data;
using Callahan.Api.DTOs;
using Callahan.Api.Models;
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
        // The other block's templates are hidden the same way: they stay in the database
        // and Start() still serves them, they just are not offered in this mode.
        var mode = await TrainingModeStore.GetAsync(_db);
        var templates = (await _db.WorkoutTemplates
            .Where(t => !t.IsRetired && (t.Block == TemplateBlock.Any || t.Block == mode))
            .OrderBy(t => t.SortOrder)
            .Select(t => new { t.Id, t.Name, t.Subtitle, t.Block })
            .ToListAsync())
            .Select(t => new WorkoutTemplateSummaryDto(t.Id, t.Name, t.Subtitle, t.Block.ToString()))
            .ToList();

        return Ok(templates);
    }

    [HttpGet("{id}/start")]
    public async Task<ActionResult<WorkoutTemplateStartDto>> Start(int id)
    {
        var template = await _db.WorkoutTemplates
            .Include(t => t.Exercises).ThenInclude(te => te.Exercise).ThenInclude(e => e.MuscleTargets)
            .FirstOrDefaultAsync(t => t.Id == id);

        if (template is null) return NotFound();

        // Prefill per exercise, not per session: the latest session of this
        // template may have skipped an exercise (or the template may have
        // gained it since), and a single "last session" would then leave it
        // blank even though an earlier session logged it.
        var exerciseIds = template.Exercises.Select(te => te.ExerciseId).ToList();
        var recentSessions = await _db.WorkoutSessions
            .Where(s => s.WorkoutTemplateId == id)
            .OrderByDescending(s => s.Date)
            .ThenByDescending(s => s.Id)
            .Select(s => new
            {
                s.Date,
                Sets = s.Sets.Where(x => exerciseIds.Contains(x.ExerciseId)).ToList()
            })
            .ToListAsync();

        // An exercise this template has never logged (a new template, or one moved between
        // templates, as with Gym A/B in program v4) falls back to the latest session of
        // ANY template that logged it, rather than starting blank. Prefill is derived from
        // logged sets, so this is the only way to "seed" it.
        var uncovered = exerciseIds
            .Where(eid => !recentSessions.Any(s => s.Sets.Any(x => x.ExerciseId == eid)))
            .ToList();
        var fallbackSessions = uncovered.Count == 0
            ? []
            : (await _db.WorkoutSessions
                .Where(s => s.WorkoutTemplateId != id && s.Sets.Any(x => uncovered.Contains(x.ExerciseId)))
                .OrderByDescending(s => s.Date)
                .ThenByDescending(s => s.Id)
                .Select(s => new
                {
                    s.Date,
                    Sets = s.Sets.Where(x => uncovered.Contains(x.ExerciseId)).ToList()
                })
                .ToListAsync())
                .Select(s => (s.Date, s.Sets))
                .ToList();

        var exercises = template.Exercises
            .OrderBy(te => te.ExerciseOrder)
            .Select(te =>
            {
                var source = recentSessions
                    .Select(s => (s.Date, s.Sets))
                    .Concat(fallbackSessions)
                    .Where(s => s.Sets.Any(x => x.ExerciseId == te.ExerciseId))
                    .Select(s => ((DateOnly Date, List<ExerciseSet> Sets)?)(s.Date, s.Sets))
                    .FirstOrDefault();
                var lastSets = source?.Sets
                    .Where(s => s.ExerciseId == te.ExerciseId)
                    .OrderBy(s => s.SetOrder)
                    .ToList() ?? [];

                var previousSets = lastSets
                    .Select(s => new PreviousSetDto(s.SetOrder, s.Reps, s.WeightKg, s.SetType.ToString(), s.DurationSeconds))
                    .ToList();

                var primaryMuscle = te.Exercise.MuscleTargets.Where(mt => mt.IsPrimary).Select(mt => mt.MuscleGroup.ToString()).FirstOrDefault();

                var readiness = ProgressionReadinessChecker.Evaluate(
                    new ProgressionReadinessChecker.SlotInput(te.TargetSets, te.TargetRepsMax),
                    source?.Date,
                    lastSets
                        .Select(s => new ProgressionReadinessChecker.SetInput(s.SetOrder, s.Reps, s.SetType))
                        .ToList());

                return new WorkoutTemplateExerciseStartDto(
                    te.Id, te.ExerciseId, te.Exercise.Name, te.TargetSets, te.WarmupSets, te.TargetReps, te.RestSeconds, te.Tempo, te.Cue, primaryMuscle,
                    te.Exercise.IsAssisted, te.Exercise.IsTimeBased, te.Exercise.IsPerSide, te.Exercise.PerSideDelaySeconds, te.Exercise.IsBodyweight, te.TargetDurationSeconds, te.SupersetWithNext, te.SupersetRestSeconds, previousSets,
                    readiness.Ready, te.Exercise.ShortName);
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

    // Persists the whole superset group's rest duration. Only meaningful when
    // called on a group's first slot — the client is responsible for that;
    // this endpoint just writes whatever slot id it's given, same as
    // UpdateRestSeconds.
    [HttpPut("exercises/{workoutTemplateExerciseId}/superset-rest-seconds")]
    public async Task<IActionResult> UpdateSupersetRestSeconds(int workoutTemplateExerciseId, UpdateSupersetRestSecondsRequest request)
    {
        var te = await _db.WorkoutTemplateExercises.FindAsync(workoutTemplateExerciseId);
        if (te is null) return NotFound();
        if (request.SupersetRestSeconds < 0) return BadRequest();

        te.SupersetRestSeconds = request.SupersetRestSeconds;
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
