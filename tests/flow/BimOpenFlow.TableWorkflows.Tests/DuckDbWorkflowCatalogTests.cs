using System.Text.Json;
using Ara3D.DataFlowEngine;
using Ara3D.NodeGraph;
using BimOpenFlow.Host;

namespace BimOpenFlow.TableWorkflows.Tests;

/// <summary>
/// Every graph embedded in samples/duckdb-analyses/workflows.json (the catalog the
/// /duckdb.html demo and scripts/check-bim-flow-duckdb.mjs consume) parses and validates
/// against the tables profile registry. The demo runs them over the private Snowdon
/// export, whose tables (door, storey, space, roof, evidence, source_*) sample.duckdb
/// lacks, so only the graph that reads information_schema evaluates here, over a
/// freshly generated sample.duckdb with {DUCKDB} rewritten to it.
/// </summary>
[TestFixture]
public sealed class DuckDbWorkflowCatalogTests
{
    public const string DuckDbPlaceholder = "{DUCKDB}";

    /// <summary>One catalog entry: the analysis id and its embedded graph document.</summary>
    public sealed record CatalogGraph(string Id, string ResultNode, GraphDocument Graph);

    public static string CatalogFile
        => Path.Combine(Path.GetDirectoryName(SamplePaths.TablesDir)!, "duckdb-analyses", "workflows.json");

    /// <summary>Graph ids whose SQL names Snowdon tables that sample.duckdb does not hold.</summary>
    private static readonly IReadOnlySet<string> SnowdonOnly = new HashSet<string>
    {
        "duckdb-door-schedule", "duckdb-door-types", "duckdb-room-schedule", "duckdb-room-distribution",
        "duckdb-missing-widths", "duckdb-roof-coverage", "duckdb-evidence-trace", "duckdb-source-lineage",
    };

    public static IReadOnlyList<CatalogGraph> ReadCatalog()
    {
        using var json = JsonDocument.Parse(File.ReadAllText(CatalogFile));
        return json.RootElement.EnumerateArray()
            .Select(entry => new CatalogGraph(
                entry.GetProperty("id").GetString()!,
                entry.GetProperty("result").GetString()!,
                GraphDocumentIO.Parse(entry.GetProperty("graph").GetRawText())))
            .ToList();
    }

    public static IEnumerable<TestCaseData> Graphs
        => ReadCatalog().Select(g => new TestCaseData(g).SetArgDisplayNames(g.Id));

    public static IEnumerable<TestCaseData> SnowdonOnlyGraphs
        => Graphs.Where(t => SnowdonOnly.Contains(((CatalogGraph)t.Arguments[0]!).Id));

    public static IEnumerable<TestCaseData> SampleDbGraphs
        => Graphs.Where(t => !SnowdonOnly.Contains(((CatalogGraph)t.Arguments[0]!).Id));

    private string _dir = null!;

    private string SampleDuckDb => Path.Combine(_dir, SampleFixtures.DuckDbName);

    [OneTimeSetUp]
    public void SeedSampleDuckDb()
    {
        _dir = Path.Combine(Path.GetTempPath(), "bimopenflow-duckdb-catalog", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
        SampleFixtures.WriteDuckDb(SampleFixtures.ReadAll(SamplePaths.TablesDir), SampleDuckDb);
    }

    [OneTimeTearDown]
    public void DeleteSampleDuckDb()
    {
        try
        {
            Directory.Delete(_dir, recursive: true);
        }
        catch (IOException)
        {
        }
    }

    [Test]
    public void CatalogHoldsNineGraphsWithUniqueIds()
    {
        var ids = ReadCatalog().Select(g => g.Id).ToList();
        Assert.That(ids, Has.Count.EqualTo(9));
        Assert.That(ids, Is.Unique);
        Assert.That(ids, Has.All.Matches<string>(BimOpenFlow.Host.Store.AnalysisId.IsValid));
    }

    [Test]
    public void SnowdonOnlyListNamesCatalogGraphs()
        => Assert.That(SnowdonOnly, Is.SubsetOf(ReadCatalog().Select(g => g.Id)));

    [TestCaseSource(nameof(Graphs))]
    public void ParsesAndValidates(CatalogGraph entry)
    {
        Assert.That(entry.Graph.Nodes, Is.Not.Empty);
        Assert.That(entry.Graph.FindNode(entry.ResultNode), Is.Not.Null, "result node");
        Assert.That(entry.Graph.Validate(HostComposition.TablePacks()), Is.Empty);
    }

    [TestCaseSource(nameof(Graphs))]
    public void DatabasePathIsThePlaceholder(CatalogGraph entry)
        => Assert.That(entry.Graph.Values.Values.SelectMany(p => p.Values).Where(v => v.Contains(DuckDbPlaceholder)),
            Is.EqualTo(new[] { DuckDbPlaceholder }));

    /// <summary>These graphs SELECT from Snowdon tables, so over sample.duckdb the duck.query
    /// nodes fail with a missing-table error and everything downstream is Unavailable.</summary>
    [TestCaseSource(nameof(SnowdonOnlyGraphs))]
    public void SnowdonOnly_FailsOnlyAtTheQueriesOverSampleDb(CatalogGraph entry)
    {
        var snapshot = Evaluate(entry);
        var failed = snapshot.Results.Where(r => r.Value.Status == NodeStatus.Error).Select(r => r.Key).ToList();
        Assert.That(failed, Is.Not.Empty);
        Assert.That(failed, Has.All.Matches<string>(id => entry.Graph.FindNode(id)!.Kind == "duck.query"));
    }

    [TestCaseSource(nameof(SampleDbGraphs))]
    public void SampleDb_EvaluatesEveryNodeOk(CatalogGraph entry)
    {
        var snapshot = Evaluate(entry);
        var notOk = snapshot.Results
            .Where(r => r.Value.Status != NodeStatus.Ok)
            .Select(r => $"{r.Key}: {r.Value.Status} {r.Value.Error}")
            .ToList();
        Assert.That(notOk, Is.Empty, entry.Id);
        Assert.That(snapshot.Results[entry.ResultNode].Outputs, Is.Not.Empty);
    }

    private EvalSnapshot Evaluate(CatalogGraph entry)
        => SampleSeeding.RewritePaths(entry.Graph, DuckDbPlaceholder, SampleDuckDb)
            .Evaluate(HostComposition.TablePacks());
}
