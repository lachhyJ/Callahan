using Markdig;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Callahan.Api.Controllers;

// The program document is authored elsewhere (see docs/program-sync.md) — this
// controller just serves whatever files are configured, it doesn't own the
// content. The markdown copy is what the in-app Program page renders; the PDF
// is the archival artifact and feeds the program-PDF sync pipeline on the NAS.
[ApiController]
[Authorize]
[Route("api/[controller]")]
public class ProgramController : ControllerBase
{
    private readonly IConfiguration _config;

    // UseAdvancedExtensions pulls in GFM pipe tables, which the program doc
    // leans on heavily (every session is a table). DisableHtml escapes any raw
    // HTML in the source instead of passing it through, so the output is only
    // tags Markdig itself emits — the client still sanitises on top.
    private static readonly MarkdownPipeline MarkdownPipeline =
        new MarkdownPipelineBuilder().UseAdvancedExtensions().DisableHtml().Build();

    // The exact heading text the active-workout warm-up toggle looks for.
    // This is a contract with the program doc, not a parser to make
    // "smarter" — see docs/program-sync.md's sync checklist. If the doc's
    // author retitles or restructures this section, GetWarmup starts
    // 404ing until the checklist catches it.
    private const string WarmupHeadingPrefix = "Universal gym warm-up";

    public ProgramController(IConfiguration config)
    {
        _config = config;
    }

    // Rendered HTML for the Program page. Server-side render keeps the frontend
    // dependency-free and means WKWebView never has to display a PDF — the hack
    // that route relied on only ever worked in mobile Safari.
    [HttpGet("content")]
    public IActionResult GetContent()
    {
        var path = _config["ProgramDoc:MarkdownPath"];
        if (string.IsNullOrEmpty(path) || !System.IO.File.Exists(path))
        {
            return NotFound(new { error = "Program document isn't configured on this server." });
        }

        var markdown = System.IO.File.ReadAllText(path);
        var html = Markdown.ToHtml(markdown, MarkdownPipeline);
        return Ok(new { html });
    }

    // Just the "Universal gym warm-up" section, for the active-workout
    // warm-up toggle. It's genuinely universal across all three gym
    // templates (confirmed against the doc — there's one shared warm-up,
    // not one per day), so there's no per-template scoping to do here.
    [HttpGet("warmup")]
    public IActionResult GetWarmup()
    {
        var path = _config["ProgramDoc:MarkdownPath"];
        if (string.IsNullOrEmpty(path) || !System.IO.File.Exists(path))
        {
            return NotFound(new { error = "Program document isn't configured on this server." });
        }

        var lines = System.IO.File.ReadAllLines(path);
        var start = -1;
        var headingLevel = 0;
        for (var i = 0; i < lines.Length; i++)
        {
            var hashCount = lines[i].Length - lines[i].TrimStart('#').Length;
            if (hashCount == 0) continue;
            var heading = lines[i].TrimStart('#', ' ');
            if (heading.StartsWith(WarmupHeadingPrefix, StringComparison.OrdinalIgnoreCase))
            {
                start = i;
                headingLevel = hashCount;
                break;
            }
        }

        if (start == -1)
        {
            return NotFound(new { error = "Warm-up section not found in the program document — it may have been retitled or restructured." });
        }

        var end = lines.Length;
        for (var i = start + 1; i < lines.Length; i++)
        {
            var hashCount = lines[i].Length - lines[i].TrimStart('#').Length;
            if (hashCount > 0 && hashCount <= headingLevel)
            {
                end = i;
                break;
            }
        }

        var section = string.Join('\n', lines[(start + 1)..end]);
        var html = Markdown.ToHtml(section, MarkdownPipeline);
        return Ok(new { html });
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
