using Ara3D.DataFlowEngine;
using Ara3D.NodeGraph;
using BimOpenFlow.Host;

namespace BimOpenFlow.TableWorkflows.Tests;

/// <summary>
/// Every committed sample analysis (samples/analyses/*.json) parses, validates
/// against the tables profile registry, and evaluates all-green over a temp-dir
/// copy of the sample data (CSVs copied, binaries regenerated), so the tests
/// never depend on the committed binaries or touch the real store.
/// </summary>
[TestFixture]
public sealed class SampleAnalysesTests
{
    private string _dir = null!;

    public static IEnumerable<TestCaseData> SampleFiles
        => Directory.EnumerateFiles(SamplePaths.AnalysesDir, "*.json")
            .Order(StringComparer.Ordinal)
            .Select(f => new TestCaseData(f).SetArgDisplayNames(Path.GetFileNameWithoutExtension(f)));

    [OneTimeSetUp]
    public void SeedTempSampleData()
    {
        _dir = Path.Combine(Path.GetTempPath(), "bimopenflow-sample-analyses", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
        foreach (var csv in Directory.EnumerateFiles(SamplePaths.TablesDir, "*.csv"))
            File.Copy(csv, Path.Combine(_dir, Path.GetFileName(csv)));
        SampleFixtures.SeedAll(SamplePaths.TablesDir, _dir);
    }

    [OneTimeTearDown]
    public void DeleteTempSampleData()
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
    public void ThereAreSampleAnalyses()
        => Assert.That(SampleFiles.Count(), Is.GreaterThanOrEqualTo(4));

    [Test]
    public void SampleFileNames_AreValidAnalysisIds()
        => Assert.That(
            SampleFiles.Select(t => Path.GetFileNameWithoutExtension((string)t.Arguments[0]!)),
            Has.All.Matches<string>(BimOpenFlow.Host.Store.AnalysisId.IsValid));

    [TestCaseSource(nameof(SampleFiles))]
    public void ParsesAndValidates(string file)
    {
        var doc = GraphDocumentIO.Load(file);
        Assert.That(doc.Nodes, Is.Not.Empty);
        Assert.That(doc.Validate(HostComposition.TablePacks()), Is.Empty);
    }

    [TestCaseSource(nameof(SampleFiles))]
    public void Evaluates_EveryNodeOk(string file)
    {
        var doc = SampleSeeding.RewritePaths(GraphDocumentIO.Load(file), _dir);
        var session = TableReads.NewTableSession();
        var snapshot = session.Evaluate(doc);
        var failed = snapshot.Results
            .Where(r => r.Value.Status != NodeStatus.Ok)
            .Select(r => $"{r.Key}: {r.Value.Status} {r.Value.Error}")
            .ToList();
        Assert.That(failed, Is.Empty, $"Nodes not Ok in {Path.GetFileName(file)}");
    }
}
