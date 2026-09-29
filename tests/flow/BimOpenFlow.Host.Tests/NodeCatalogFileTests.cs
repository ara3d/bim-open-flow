using System.Text.Json;
using BimOpenFlow.Contracts;
using BimOpenFlow.Host.Api;
using BimOpenToolkit.TestSupport;

namespace BimOpenFlow.Host.Tests;

/// <summary>docs/nodes.catalog.json is what GET /api/catalog/nodes answers, over the nodes of both
/// host profiles, committed so web tests can size node cards without a running host (the sample
/// overlap check in bimopenflow/web/packages/graph). This test fails when a pack changes and the
/// file is not regenerated; set BOF_WRITE_NODE_CATALOG=1 and rerun it to rewrite the file.</summary>
public sealed class NodeCatalogFileTests
{
    private static readonly string CatalogPath = Path.Combine(RepoPaths.Root, "docs", "nodes.catalog.json");

    private static readonly JsonSerializerOptions Indented = new(ApiJson.Options) { WriteIndented = true };

    /// <summary>The union of the bim and tables profiles' catalogs, in the host's order.</summary>
    private static string ExpectedText()
    {
        var nodes = new[] { HostComposition.AllPacks(), HostComposition.TablePacks() }
            .SelectMany(registry => registry.ToCatalog().Nodes)
            .DistinctBy(node => (node.Kind, node.Version))
            .OrderBy(node => node.Kind, StringComparer.Ordinal)
            .ThenBy(node => node.Version)
            .ToList();
        return JsonSerializer.Serialize(new NodeCatalog(nodes), Indented).ReplaceLineEndings("\n") + "\n";
    }

    [Test]
    public void CommittedCatalog_MatchesThePacks()
    {
        var expected = ExpectedText();
        if (Environment.GetEnvironmentVariable("BOF_WRITE_NODE_CATALOG") == "1")
            File.WriteAllText(CatalogPath, expected);
        var committed = File.Exists(CatalogPath) ? File.ReadAllText(CatalogPath).ReplaceLineEndings("\n") : "";
        Assert.That(committed, Is.EqualTo(expected),
            $"{CatalogPath} is stale: rerun this test with BOF_WRITE_NODE_CATALOG=1 and commit the file");
    }
}
