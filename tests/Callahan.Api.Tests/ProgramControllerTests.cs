using Callahan.Api.Controllers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;

namespace Callahan.Api.Tests;

// GetContent reads the configured markdown file and hands back rendered HTML.
// What matters: a configured, present file renders (including GFM tables, which
// the program doc is built from); a missing or unconfigured path is a clean 404
// rather than a 500.
public class ProgramControllerTests
{
    private static ProgramController ControllerWith(string? markdownPath)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ProgramDoc:MarkdownPath"] = markdownPath,
            })
            .Build();
        return new ProgramController(config);
    }

    [Fact]
    public void GetContentRendersMarkdownIncludingTables()
    {
        var tmp = Path.GetTempFileName();
        File.WriteAllText(tmp, "# Gym 1\n\n| Exercise | Sets |\n|---|---|\n| Trap Bar Deadlift | 4x4 |\n");
        try
        {
            var result = ControllerWith(tmp).GetContent();

            var ok = Assert.IsType<OkObjectResult>(result);
            var html = (string)ok.Value!.GetType().GetProperty("html")!.GetValue(ok.Value)!;
            Assert.Contains("<h1", html);
            Assert.Contains("<table>", html);
            Assert.Contains("Trap Bar Deadlift", html);
        }
        finally
        {
            File.Delete(tmp);
        }
    }

    [Fact]
    public void GetContentIsNotFoundWhenTheFileIsMissing()
    {
        var result = ControllerWith(Path.Combine(Path.GetTempPath(), "callahan-no-such-program.md")).GetContent();
        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public void GetContentIsNotFoundWhenUnconfigured()
    {
        var result = ControllerWith(null).GetContent();
        Assert.IsType<NotFoundObjectResult>(result);
    }

    private const string V4Shape = """
        ## Every session

        ### Universal gym warm-up (10 mins)

        Ankle circuit, then leg swings.

        ### Universal field warm-up (10-12 mins)

        Jog.

        ## In-season

        ### Field 1 — Defence & Acceleration (~30-35 mins)

        | Block | Prescription |
        |---|---|
        | Vertical jump to target | 3x3 |

        ### Gym A — Lower & Push

        Trap bar.

        ## No-club block

        ### Field 1

        Same session as in-season, done on Monday.

        ### Field 2 - Repeat Effort & COD (~45 mins)

        Cut drill.

        **Finisher rotation (one per week):**

        | Format | Prescription |
        |---|---|
        | Point simulation | 6-8 |

        ### Aerobic

        Easy.
        """;

    private static T WithDoc<T>(string markdown, Func<ProgramController, T> act)
    {
        var tmp = Path.GetTempFileName();
        File.WriteAllText(tmp, markdown);
        try { return act(ControllerWith(tmp)); }
        finally { File.Delete(tmp); }
    }

    private static List<(string Key, string? Title, string? Html)> Sessions(IActionResult result)
    {
        var ok = Assert.IsType<OkObjectResult>(result);
        var list = (System.Collections.IEnumerable)ok.Value!.GetType().GetProperty("sessions")!.GetValue(ok.Value)!;
        return list.Cast<object>().Select(o => (
            (string)o.GetType().GetProperty("key")!.GetValue(o)!,
            (string?)o.GetType().GetProperty("title")!.GetValue(o),
            (string?)o.GetType().GetProperty("html")!.GetValue(o))).ToList();
    }

    [Fact]
    public void FieldSessionsReturnTheRealSectionsAndSkipTheNoClubStub()
    {
        var sessions = WithDoc(V4Shape, c => Sessions(c.GetFieldSessions()));

        var f1 = sessions.Single(x => x.Key == "field-1");
        Assert.StartsWith("Field 1 — Defence", f1.Title);
        Assert.Contains("Vertical jump to target", f1.Html);
        Assert.DoesNotContain("Same session as in-season", f1.Html);
        Assert.DoesNotContain("Trap bar", f1.Html); // stops at the next ### heading

        var f2 = sessions.Single(x => x.Key == "field-2");
        Assert.Contains("Cut drill", f2.Html);
        Assert.Contains("Point simulation", f2.Html); // bold lead-in + table stay inside the section
        Assert.DoesNotContain("Easy.", f2.Html);
    }

    [Fact]
    public void ABareFieldHeadingEarlierInTheDocDoesNotShadowTheRealSection()
    {
        // Reordered doc: the no-club stub now comes first. Without the dash rule it would win.
        var doc = "## No-club\n\n### Field 1\n\nSame session as in-season.\n\n## In-season\n\n### Field 1 - Defence\n\nReal plan.\n";
        var f1 = WithDoc(doc, c => Sessions(c.GetFieldSessions())).Single(x => x.Key == "field-1");

        Assert.Contains("Real plan", f1.Html);
        Assert.DoesNotContain("Same session", f1.Html);
    }

    [Fact]
    public void AMissingFieldSectionComesBackAsNullNotAnError()
    {
        var sessions = WithDoc("## Only this\n\n### Field 1 — Defence\n\nStuff.\n", c => Sessions(c.GetFieldSessions()));

        Assert.NotNull(sessions.Single(x => x.Key == "field-1").Html);
        var f2 = sessions.Single(x => x.Key == "field-2");
        Assert.Null(f2.Html);
        Assert.Null(f2.Title);
    }

    [Fact]
    public void FieldSessionsAreNotFoundWhenTheDocIsUnconfigured()
    {
        Assert.IsType<NotFoundObjectResult>(ControllerWith(null).GetFieldSessions());
    }

    [Fact]
    public void WarmupStillStopsAtTheNextSiblingHeading()
    {
        var html = WithDoc(V4Shape, c =>
        {
            var ok = Assert.IsType<OkObjectResult>(c.GetWarmup());
            return (string)ok.Value!.GetType().GetProperty("html")!.GetValue(ok.Value)!;
        });

        Assert.Contains("Ankle circuit, then leg swings", html);
        Assert.DoesNotContain("Jog.", html);
    }

    [Fact]
    public void WarmupIsNotFoundWhenTheHeadingIsGone()
    {
        Assert.IsType<NotFoundObjectResult>(WithDoc("## Nothing\n", c => c.GetWarmup()));
    }
}
