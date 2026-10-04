using Ara3D.DataFlowEngine.TestKit;

namespace BimOpenFlow.GraphText.Tests;

/// <summary>The status line under each binding names the cause of anything that is not Ok.</summary>
[TestFixture]
public sealed class StatusTests
{
    private static string Print(GraphBuilder graph, GraphTextOptions? options = null)
        => Fixtures.Print(graph.Build(), TestNodes.Registry, options);

    [Test]
    public void Error_PrintsTheEngineMessage_AndDownstreamNamesTheBlocker()
    {
        var text = Print(Graph
            .Node("c", "test.const", ("value", "1"))
            .Node("boom", "test.throw")
            .Node("after", "test.negate")
            .Connect("c.out", "boom.in")
            .Connect("boom.out", "after.in"));
        Assert.Multiple(() =>
        {
            Assert.That(text, Does.Contain("boom = test.throw@1(in: c.out);\n  // Error  InvalidOperationException: test.throw always fails\n"));
            Assert.That(text, Does.Contain("after = test.negate@1(in: boom.out);\n  // Unavailable  blocked by boom (Error)\n"));
        });
    }

    [Test]
    public void Unready_NamesTheUnconnectedInput_AndDownstreamNamesTheOrigin()
    {
        var text = Print(Graph
            .Node("sum", "test.add")
            .Node("neg", "test.negate")
            .Node("c", "test.const", ("value", "1"))
            .Connect("c.out", "sum.a")
            .Connect("sum.out", "neg.in"));
        Assert.Multiple(() =>
        {
            Assert.That(text, Does.Contain("sum = test.add@1(a: c.out);\n  // Unready  input b not connected\n"));
            Assert.That(text, Does.Contain("neg = test.negate@1(in: sum.out);\n  // Unready  waits on sum (Unready)\n"));
        });
    }

    [Test]
    public void EffectNodes_StayPending()
        => Assert.That(Print(Graph
                .Node("c", "test.const", ("value", "1"))
                .Node("write", "test.effect")
                .Connect("c.out", "write.in")),
            Does.Contain("write = test.effect@1(in: c.out);\n  // EffectPending  not executed outside a Run\n"));

    [Test]
    public void Warnings_FollowTheStatusLine()
        => Assert.That(Print(Graph
                .Node("c", "test.const", ("value", "1"))
                .Node("w", "test.warn")
                .Connect("c.out", "w.in")),
            Does.Contain("w = test.warn@1(in: c.out);\n  // Ok  Integer 1\n  //   warn careful\n"));

    [Test]
    public void DebugMode_AddsExecutionCountsAndFullOutputHashes()
    {
        var text = Print(Graph.Node("c", "test.const", ("kind", "Number"), ("value", "3.14159265")),
            GraphTextOptions.Golden with { Mode = GraphTextMode.Debug });
        Assert.Multiple(() =>
        {
            Assert.That(text, Does.Contain("// Ok  exec 1  Number 3.14159265"));
            Assert.That(text, Does.Match(@"//   hash [0-9a-f]{64}\n"));
            Assert.That(text, Does.Contain("   debug\n"));
        });
    }

    [Test]
    public void PathAliases_ReplaceAbsoluteDirectoriesInEveryLine()
    {
        var dir = Path.Combine(Path.GetTempPath(), "graph-text-alias");
        var options = GraphTextOptions.Golden with { PathAliases = [new(dir, "{SAMPLES}")] };
        var text = Print(Graph.Node("p", "test.const", ("kind", "Text"), ("value", Path.Combine(dir, "a.csv").Replace('\\', '/'))), options);
        Assert.Multiple(() =>
        {
            Assert.That(text, Does.Contain("value: \"{SAMPLES}/a.csv\""));
            Assert.That(text, Does.Contain("Text \"{SAMPLES}/a.csv\""));
            Assert.That(text, Does.Not.Contain(Path.GetTempPath().Replace('\\', '/').TrimEnd('/')));
        });
    }
}
