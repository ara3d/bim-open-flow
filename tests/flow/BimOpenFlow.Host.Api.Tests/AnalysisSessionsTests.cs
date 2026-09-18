using Ara3D.DataFlowEngine;
using Ara3D.DataFlowEngine.Abstractions;
using Ara3D.DataFlowEngine.TestKit;
using Ara3D.NodeGraph;
using BimOpenFlow.Host.Store;

namespace BimOpenFlow.Host.Api.Tests;

/// <summary>Reevaluate re-runs open sessions over unchanged documents: a node that failed
/// because its data was not ready recovers once the data is, observers see the pass, and
/// results that were already Ok are served from the memo rather than executed again.</summary>
[TestFixture]
public sealed class AnalysisSessionsTests
{
    /// <summary>Fails while Ready is false; then emits its input like test.probe.</summary>
    private sealed class Gate
    {
        public bool Ready;
        public int Executions;

        public IFlowNode Node => new DelegateNode(
            new("test.gate", 1, NodeCapability.Pure,
                [new PortSpec("in", PortType.Any)], [new PortSpec("out", PortType.Any)], []),
            (_, inputs, _) =>
            {
                Executions++;
                return Ready ? [inputs[0]] : throw new InvalidOperationException("not ready yet");
            });
    }

    private string _storeDir = null!;

    [SetUp]
    public void NewStore()
    {
        _storeDir = Path.Combine(Path.GetTempPath(), "bof-sessions-tests", Guid.NewGuid().ToString("N"));
    }

    [TearDown]
    public void DeleteStore()
    {
        try { Directory.Delete(_storeDir, recursive: true); }
        catch (IOException) { }
    }

    private static GraphDocument ConstGate()
        => Graph
            .Node("c", "test.const", ("kind", "Integer"), ("value", "7"))
            .Node("g", "test.gate")
            .Connect("c.out", "g.in")
            .Build();

    [Test]
    public void Reevaluate_RecoversAFailedNode_WithoutTouchingTheDocument()
    {
        var gate = new Gate();
        var store = new AnalysisStore(_storeDir);
        var registry = NodeRegistry.Combine(TestNodes.All, [gate.Node]);
        var sessions = new AnalysisSessions(store, registry);
        store.Save("a", ConstGate());
        var passes = new List<EvalSnapshot>();
        using var subscription = sessions.Subscribe("a", passes.Add);

        Assert.That(sessions.Snapshot("a").Results["g"].Status, Is.EqualTo(NodeStatus.Error));
        var constExecutions = sessions.Snapshot("a").Results["c"].ExecutionCount;

        gate.Ready = true;
        sessions.Reevaluate();

        var after = sessions.Snapshot("a");
        Assert.Multiple(() =>
        {
            Assert.That(after.Results["g"].Status, Is.EqualTo(NodeStatus.Ok));
            Assert.That(after.Results["c"].ExecutionCount, Is.EqualTo(constExecutions), "an Ok result is memoized, not re-run");
            Assert.That(gate.Executions, Is.EqualTo(2), "the failed node ran once before and once after");
            Assert.That(passes, Has.Count.GreaterThanOrEqualTo(1), "observers see the re-evaluation pass");
            Assert.That(store.Load("a"), Is.EqualTo(ConstGate()), "the stored document is untouched");
        });
    }

    [Test]
    public void Reevaluate_WithNoOpenSessions_DoesNothing()
    {
        var sessions = new AnalysisSessions(new AnalysisStore(_storeDir), TestNodes.Registry);
        Assert.DoesNotThrow(sessions.Reevaluate);
    }
}
