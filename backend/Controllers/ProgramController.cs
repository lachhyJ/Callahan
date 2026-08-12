using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Callahan.Api.Controllers;

// The program PDF is the actual authoring surface (see docs/program-sync.md) —
// this just serves whatever file is configured, it doesn't own the content.
[ApiController]
[Authorize]
[Route("api/[controller]")]
public class ProgramController : ControllerBase
{
    private readonly IConfiguration _config;

    public ProgramController(IConfiguration config)
    {
        _config = config;
    }

    [HttpGet("pdf")]
    public IActionResult GetPdf()
    {
        var path = _config["ProgramPdf:Path"];
        if (string.IsNullOrEmpty(path) || !System.IO.File.Exists(path))
        {
            return NotFound(new { error = "Program PDF isn't configured on this server." });
        }

        var stream = System.IO.File.OpenRead(path);
        return File(stream, "application/pdf");
    }
}
