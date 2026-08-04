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
public class ExercisesController : ControllerBase
{
    private readonly AppDbContext _db;

    public ExercisesController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<ActionResult<List<ExerciseDto>>> GetAll()
    {
        var exercises = await _db.Exercises
            .OrderBy(e => e.Category).ThenBy(e => e.Name)
            .Select(e => new ExerciseDto(e.Id, e.Name, e.Category.ToString()))
            .ToListAsync();

        return Ok(exercises);
    }

    [HttpPost]
    public async Task<ActionResult<ExerciseDto>> Create(CreateExerciseRequestDto request)
    {
        if (!Enum.TryParse<ExerciseCategory>(request.Category, ignoreCase: true, out var category))
        {
            return BadRequest(new { error = $"Unknown category '{request.Category}'." });
        }

        var exercise = new Exercise { Name = request.Name, Category = category };
        _db.Exercises.Add(exercise);
        await _db.SaveChangesAsync();

        return Ok(new ExerciseDto(exercise.Id, exercise.Name, exercise.Category.ToString()));
    }
}
