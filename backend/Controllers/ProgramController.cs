using System.Text.RegularExpressions;
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
        var found = FindSection(lines, h => h.StartsWith(WarmupHeadingPrefix, StringComparison.OrdinalIgnoreCase));
        if (found is null)
        {
            return NotFound(new { error = "Warm-up section not found in the program document — it may have been retitled or restructured." });
        }

        var html = Markdown.ToHtml(found.Value.Body, MarkdownPipeline);
        return Ok(new { html });
    }

    // "Field 1 — Defence & Acceleration" and "Field 2 — Repeat Effort & COD": the
    // number, then any dash, then a title. The dash is what tells the real section
    // from the bare "### Field 1" stub in the no-club block ("same session as
    // in-season"), and tolerating em dash, en dash and hyphen means retyping it on a
    // phone keyboard doesn't break the match. Same contract as the warm-up: this
    // is looked up by heading text, and a retitled section shows up as "not found".
    private static readonly (string Key, Regex Heading)[] FieldSessions =
    [
        ("field-1", new Regex(@"^Field\s+1\s*[—–-]\s*\S", RegexOptions.IgnoreCase)),
        ("field-2", new Regex(@"^Field\s+2\s*[—–-]\s*\S", RegexOptions.IgnoreCase)),
    ];

    // The field-session plans, for the Routines page: pulled from the program doc
    // rather than copied into the database, so a v4 edit shows up with no data
    // change. A section that can't be found comes back with html null, so the page
    // can say so instead of silently showing less.
    [HttpGet("field-sessions")]
    public IActionResult GetFieldSessions()
    {
        var path = _config["ProgramDoc:MarkdownPath"];
        if (string.IsNullOrEmpty(path) || !System.IO.File.Exists(path))
        {
            return NotFound(new { error = "Program document isn't configured on this server." });
        }

        var lines = System.IO.File.ReadAllLines(path);
        var sessions = FieldSessions.Select(f =>
        {
            var found = FindSection(lines, h => f.Heading.IsMatch(h));
            return new
            {
                key = f.Key,
                title = found?.Heading,
                html = found is null ? null : Markdown.ToHtml(found.Value.Body, MarkdownPipeline),
            };
        }).ToList();

        return Ok(new { sessions });
    }

    // First heading satisfying the predicate, and everything under it up to the next
    // heading of the same or a higher level. Heading is the text without the #s.
    private static (string Heading, string Body)? FindSection(string[] lines, Func<string, bool> headingMatches)
    {
        var start = -1;
        var headingLevel = 0;
        string heading = "";
        for (var i = 0; i < lines.Length; i++)
        {
            var hashCount = lines[i].Length - lines[i].TrimStart('#').Length;
            if (hashCount == 0) continue;
            var text = lines[i].TrimStart('#', ' ');
            if (headingMatches(text))
            {
                start = i;
                headingLevel = hashCount;
                heading = text.Trim();
                break;
            }
        }

        if (start == -1) return null;

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

        return (heading, string.Join('\n', lines[(start + 1)..end]));
    }

}
