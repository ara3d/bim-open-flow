using Ara3D.DataFlowEngine.Abstractions;
using Ara3D.DataFlowEngine.Runs;
using Ara3D.NodeGraph;

namespace BimOpenFlow.Host.Store.Tests;

public static class StoreTestData
{
    public static GraphDocument Doc(string path = "x.bos")
        => new(
            new[] { new GraphNode("a", "source.model", 1) },
            Array.Empty<GraphEdge>(),
            new Dictionary<string, IReadOnlyDictionary<string, string>>
            {
                ["a"] = new Dictionary<string, string> { ["path"] = path },
            },
            new Dictionary<string, NodeLayout>());

    public static RunRecord Run(string timestampUtc = "2026-08-31T12:00:00.123Z", char hashDigit = 'a')
        => new(
            new string(hashDigit, 64),
            "test-engine 0.1.0",
            timestampUtc,
            Array.Empty<RunInput>(),
            new Dictionary<string, string>(),
            new Dictionary<string, FlowValue>(),
            Array.Empty<EffectRecord>(),
            Array.Empty<string>());
}

/// <summary>Base fixture: a fresh temp root per test, removed on teardown.</summary>
public abstract class StoreFixture
{
    protected string RootDir = "";
    protected AnalysisStore Store = null!;

    [SetUp]
    public void SetUp()
    {
        RootDir = Path.Combine(Path.GetTempPath(), "store-tests-" + Guid.NewGuid().ToString("N"));
        Store = new AnalysisStore(RootDir);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(RootDir))
            Directory.Delete(RootDir, recursive: true);
    }
}
