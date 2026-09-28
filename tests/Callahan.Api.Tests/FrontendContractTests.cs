using System.Text.RegularExpressions;
using Callahan.Api.Models;

namespace Callahan.Api.Tests;

// The frontend keeps its own label for each SetType (SET_TYPE_LABELS in
// frontend/src/utils/format.js); a type added here without one renders as
// "undefined" on every set row. Checked from this side because this is the side
// that changes - the same approach scripts/ultimate-stream-explore/diagnose.py
// takes to keep its constants in step with FieldGeometryOptions.
public class FrontendContractTests
{
    private static string RepoFile(string relative)
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, relative);
            if (File.Exists(candidate)) return candidate;
        }
        throw new FileNotFoundException($"Couldn't find {relative} above {AppContext.BaseDirectory}");
    }

    [Fact]
    public void EverySetTypeHasAFrontendLabel()
    {
        var source = File.ReadAllText(RepoFile("frontend/src/utils/format.js"));
        var map = Regex.Match(source, @"export const SET_TYPE_LABELS = \{([^}]*)\}");
        Assert.True(map.Success, "SET_TYPE_LABELS not found in format.js - update this test if it moved or changed shape.");
        var keys = Regex.Matches(map.Groups[1].Value, @"(\w+)\s*:").Select(m => m.Groups[1].Value).Order();

        Assert.Equal(Enum.GetNames<SetType>().Order(), keys);
    }
}
