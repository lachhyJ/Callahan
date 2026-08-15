using Callahan.Api.Data;
using Callahan.Api.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Callahan.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class RunSessionTypesController : ControllerBase
{
    private readonly AppDbContext _db;

    public RunSessionTypesController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<ActionResult<List<RunSessionTypeDto>>> GetAll()
    {
        var types = await _db.RunSessionTypes
            .OrderBy(t => t.SortOrder)
            .Select(t => new RunSessionTypeDto(t.Id, t.Name))
            .ToListAsync();

        return Ok(types);
    }
}
