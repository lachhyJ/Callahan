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
public class RoutinesController : ControllerBase
{
    // How much completion history to hand back with a routine. Enough to render
    // "last done" and a short notes list without paging; these are read whole,
    // by routine, and never queried piecemeal.
    private const int RecentCompletions = 30;

    private readonly AppDbContext _db;

    public RoutinesController(AppDbContext db)
    {
        _db = db;
    }

    // The training day, not the calendar day - the same 3am cutoff the frontend
    // applies in dateUtils.trainingDayIso. An ankle circuit done at 00:30 is the
    // tail of the previous day's training, and if this used the raw calendar day
    // the tick would land on a different date than everything else in the app
    // records for the same session.
    private const int TrainingDayCutoffHour = 3;

    private static DateOnly TrainingDay(DateTime now)
    {
        var day = DateOnly.FromDateTime(now);
        return now.Hour < TrainingDayCutoffHour ? day.AddDays(-1) : day;
    }

    private static DateOnly Today() => TrainingDay(DateTime.Now);

    private static RoutineDto ToDto(Routine r, DateOnly today)
    {
        var completions = r.Completions
            .OrderByDescending(c => c.Date)
            .Take(RecentCompletions)
            .Select(c => new RoutineCompletionDto(c.Id, c.Date, c.Notes))
            .ToList();

        return new RoutineDto(
            r.Id,
            r.Name,
            r.Purpose,
            r.Cadence,
            r.Notes,
            r.Items.OrderBy(i => i.ItemOrder)
                .Select(i => new RoutineItemDto(i.Id, i.ItemOrder, i.Name, i.Prescription, i.Cue))
                .ToList(),
            completions.FirstOrDefault(c => c.Date == today),
            completions);
    }

    [HttpGet]
    public async Task<ActionResult<List<RoutineDto>>> GetAll()
    {
        var routines = await _db.Routines
            .Include(r => r.Items)
            .Include(r => r.Completions)
            .OrderBy(r => r.SortOrder)
            .ToListAsync();

        var today = Today();
        return Ok(routines.Select(r => ToDto(r, today)).ToList());
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<RoutineDto>> Get(int id)
    {
        var routine = await _db.Routines
            .Include(r => r.Items)
            .Include(r => r.Completions)
            .FirstOrDefaultAsync(r => r.Id == id);

        if (routine is null) return NotFound();

        return Ok(ToDto(routine, Today()));
    }

    // Marking a day done twice is the obvious thing to do by accident - tapping
    // the button again, or editing the note afterwards - so this upserts rather
    // than failing on the unique index.
    [HttpPost("{id}/completions")]
    public async Task<ActionResult<RoutineCompletionDto>> MarkDone(int id, MarkRoutineDoneRequest request)
    {
        if (!await _db.Routines.AnyAsync(r => r.Id == id)) return NotFound();

        var date = request.Date ?? Today();

        var existing = await _db.RoutineCompletions
            .FirstOrDefaultAsync(c => c.RoutineId == id && c.Date == date);

        if (existing is null)
        {
            existing = new RoutineCompletion { RoutineId = id, Date = date, Notes = request.Notes };
            _db.RoutineCompletions.Add(existing);
        }
        else if (request.Notes is not null)
        {
            // A null Notes on a repeat call means "no note supplied", not "clear
            // the note" - the frontend sends the whole request for a plain tap.
            existing.Notes = request.Notes;
        }

        await _db.SaveChangesAsync();

        return Ok(new RoutineCompletionDto(existing.Id, existing.Date, existing.Notes));
    }

    [HttpDelete("{id}/completions/{date}")]
    public async Task<IActionResult> Undo(int id, DateOnly date)
    {
        var existing = await _db.RoutineCompletions
            .FirstOrDefaultAsync(c => c.RoutineId == id && c.Date == date);

        if (existing is null) return NotFound();

        _db.RoutineCompletions.Remove(existing);
        await _db.SaveChangesAsync();

        return NoContent();
    }
}
