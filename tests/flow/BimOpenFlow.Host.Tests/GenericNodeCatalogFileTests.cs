using BimOpenFlow.TestSupport;
using BimOpenFlow.Host.Api;

namespace BimOpenFlow.Host.Tests;

/// <summary>The graph package's committed node catalog is what the generic host's GET
/// /api/catalog/nodes answers (the tables profile only, no BIM pack), so the sample overlap and
/// text overflow tests in bimopenflow/web/packages/graph size node cards without a host, in the
/// toolkit and in bim-open-flow alike. The studio's catalog, with the BIM packs, is
/// docs/nodes.catalog.json. This test fails when a generic pack changes and the file is not
/// regenerated; rerun it with BOF_WRITE_NODE_CATALOG=1 and commit the file.</summary>
public sealed class GenericNodeCatalogFileTests
{
    public static readonly string CatalogPath =
        Path.Combine(RepoPaths.Root, "bimopenflow", "web", "packages", "graph", "test", NodeCatalogFile.FileName);

    [Test]
    public void CommittedCatalog_MatchesTheGenericPacks()
    {
        var expected = NodeCatalogFile.Text(HostComposition.Generic);
        if (Environment.GetEnvironmentVariable("BOF_WRITE_NODE_CATALOG") == "1")
            File.WriteAllText(CatalogPath, expected);
        var committed = File.Exists(CatalogPath) ? File.ReadAllText(CatalogPath).ReplaceLineEndings("\n") : "";
        Assert.That(committed, Is.EqualTo(expected),
            $"{CatalogPath} is stale: rerun this test with BOF_WRITE_NODE_CATALOG=1 and commit the file");
    }

    [Test]
    public void GenericCatalog_HasNoBimKind()
    {
        var kinds = HostComposition.Generic.All.SelectMany(p => p.Registry().ToCatalog().Nodes).Select(n => n.Kind);
        Assert.That(kinds.Where(k => k.StartsWith("bos.") || k.StartsWith("bim.") || k.StartsWith("view3d.")), Is.Empty);
    }
}
