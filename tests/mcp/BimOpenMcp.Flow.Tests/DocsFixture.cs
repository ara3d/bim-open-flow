namespace BimOpenMcp.Flow.Tests;

/// <summary>A small repository of documents for the doc-tool tests: one file in each allowlisted
/// place (README.md, PROJECT.md, docs/, samples/*/README.md, tickets/) and some that are not.</summary>
public static class DocsFixture
{
    /// <summary>Writes the fixture to a new temporary folder and returns its path.</summary>
    public static string Write()
    {
        var root = Path.Combine(Path.GetTempPath(), "bof-docs-" + Guid.NewGuid().ToString("N"));
        foreach (var (path, text) in Files)
        {
            var full = Path.Combine(root, path);
            Directory.CreateDirectory(Path.GetDirectoryName(full)!);
            File.WriteAllText(full, text);
        }
        return root;
    }

    /// <summary>PROJECT.md is long enough to page (more than 2,000 characters).</summary>
    public static readonly IReadOnlyList<(string Path, string Text)> Files =
    [
        ("README.md", "# Fixture repository\n\nA README at the root.\n"),
        ("PROJECT.md", "# Project brief\n\n" + string.Concat(Enumerable.Repeat("A line of the brief that pads it past two thousand characters.\n", 50))),
        ("docs/OVERVIEW.md", "# Overview\n\nRun `npm run demo:walkthrough` to capture the figures.\n"),
        ("samples/tables/README.md", "# Tables\n\nCSV files the sample graphs read.\n"),
        ("tickets/TKT-127-ask-about-the-toolkit.md", "---\nid: TKT-127\ntitle: Let the studio Ask agent answer questions about the toolkit\n---\n\nTKT-127 body.\n"),
        ("data/notes.md", "# Not readable: data/ is outside the allowlist\n"),
        ("src/mcp/BimOpenMcp.Flow/RepoDocs.cs", "// not readable\n"),
    ];
}
