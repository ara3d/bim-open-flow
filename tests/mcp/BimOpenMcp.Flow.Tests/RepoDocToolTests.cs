using System.Text.Json;
using Ara3D.MCP;
using BimOpenMcp.Flow;

namespace BimOpenMcp.Flow.Tests;

/// <summary>TKT-127: searchDocs and readDoc read the allowlisted documents of this checkout and
/// refuse every other path.</summary>
public sealed class RepoDocToolTests
{
    private static readonly RepoDocs Docs = RepoDocs.Locate()
        ?? throw new InvalidOperationException("The tests run from a repository checkout.");

    private static JsonElement Json(object payload)
    {
        using var doc = JsonDocument.Parse(McpJson.Serialize(payload));
        return doc.RootElement.Clone();
    }

    [TestCase("README.md")]
    [TestCase("PROJECT.md")]
    [TestCase("docs/OVERVIEW.md")]
    [TestCase("docs\\OVERVIEW.md")]
    [TestCase("samples/nrc/README.md")]
    [TestCase("tickets/TKT-127-ask-about-the-toolkit.md")]
    public void ReadDoc_ReturnsAllowedDocuments(string path)
    {
        var result = Json(FlowRepoDocTools.Read(Docs, path));
        Assert.Multiple(() =>
        {
            Assert.That(result.GetProperty("text").GetString(), Is.Not.Empty);
            Assert.That(result.GetProperty("path").GetString(), Is.EqualTo(path.Replace('\\', '/')));
        });
    }

    [TestCase("data/snowdon.duckdb")]
    [TestCase("data/notes.md")]
    [TestCase("src/mcp/BimOpenMcp.Flow/RepoDocs.cs")]
    [TestCase("src/studio/BimOpenFlow.Ask/README.md")]
    [TestCase("docs/../src/studio/BimOpenFlow.Ask/README.md")]
    [TestCase("../README.md")]
    [TestCase("./README.md")]
    [TestCase("samples/nrc/duplex-enriched.ifc")]
    [TestCase("samples/nrc/questions.txt")]
    [TestCase("docs/proposals/bim-query-platform/contract/generated/obj/project.assets.json")]
    [TestCase("/README.md")]
    [TestCase("C:/Windows/win.ini")]
    [TestCase("C:README.md")]
    [TestCase("\\\\server\\share\\README.md")]
    public void ReadDoc_RefusesEveryOtherPath(string path)
    {
        Assert.That(RepoDocs.Allowed(path), Is.Null);
        Assert.Throws<UnauthorizedAccessException>(() => FlowRepoDocTools.Read(Docs, path));
    }

    [Test]
    public void ReadDoc_AbsolutePathInsideTheRepositoryIsRefused()
        => Assert.Throws<UnauthorizedAccessException>(
            () => FlowRepoDocTools.Read(Docs, Path.Combine(Docs.Root, "README.md")));

    [Test]
    public void ReadDoc_PagesALongDocument()
    {
        var first = Json(FlowRepoDocTools.Read(Docs, "PROJECT.md", 0, 1000));
        var next = first.GetProperty("nextOffset").GetInt32();
        var second = Json(FlowRepoDocTools.Read(Docs, "PROJECT.md", next, 1000));
        Assert.Multiple(() =>
        {
            Assert.That(first.GetProperty("text").GetString(), Has.Length.EqualTo(1000));
            Assert.That(next, Is.EqualTo(1000));
            Assert.That(second.GetProperty("offset").GetInt32(), Is.EqualTo(1000));
            Assert.That(first.GetProperty("totalChars").GetInt32(), Is.GreaterThan(2000));
        });
    }

    [Test]
    public void Files_StayInsideTheAllowlist()
    {
        var files = Docs.Files();
        Assert.Multiple(() =>
        {
            Assert.That(files, Does.Contain("README.md"));
            Assert.That(files, Does.Contain("docs/OVERVIEW.md"));
            Assert.That(files, Does.Contain("samples/nrc/README.md"));
            Assert.That(files.Where(f => f.StartsWith("tickets/", StringComparison.Ordinal)), Is.Not.Empty);
            Assert.That(files.Where(f => RepoDocs.Allowed(f) is null), Is.Empty);
        });
    }

    [Test]
    public void SearchDocs_FindsTheNrcWalkthrough()
    {
        var result = Json(FlowRepoDocTools.Search(Docs, "nrc:walkthrough"));
        var files = result.GetProperty("files").EnumerateArray().ToList();
        Assert.Multiple(() =>
        {
            Assert.That(files, Is.Not.Empty);
            Assert.That(files.SelectMany(f => f.GetProperty("lines").EnumerateArray())
                .Select(l => l.GetProperty("text").GetString()), Has.Some.Contains("nrc:walkthrough"));
        });
    }

    [Test]
    public void SearchDocs_UnderLimitsToOneFolderAndReturnsTitles()
    {
        var result = Json(FlowRepoDocTools.Search(Docs, "TKT-127", "tickets/"));
        var files = result.GetProperty("files").EnumerateArray().ToList();
        Assert.Multiple(() =>
        {
            Assert.That(files.Select(f => f.GetProperty("path").GetString()), Has.All.StartWith("tickets/"));
            Assert.That(files[0].GetProperty("path").GetString(), Does.StartWith("tickets/TKT-127"));
            Assert.That(files[0].GetProperty("title").GetString(), Does.StartWith("Let the studio Ask agent"));
        });
    }
}
