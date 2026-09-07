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
}
