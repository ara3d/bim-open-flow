using Ara3D.DataFlowEngine;
using Ara3D.DataFlowEngine.Abstractions;
using Ara3D.DataFlowEngine.TestKit;
using Ara3D.DataTable;
using BimOpenFlow.Contracts;
using NodeCapability = Ara3D.DataFlowEngine.Abstractions.NodeCapability;
using ParamKind = Ara3D.DataFlowEngine.Abstractions.ParamKind;
using PortType = Ara3D.DataFlowEngine.Abstractions.PortType;

namespace BimOpenFlow.Host.Api.Tests;

/// <summary>SuggestEndpoints.Resolve against snapshots of small fake graphs.</summary>
public sealed class SuggestResolveTests
{
    private static IDataTable SampleTable()
    {
        var builder = new DataTableBuilder("t");
        builder.AddColumn(new object?[] { "w1" }, "name", typeof(string));
        builder.AddColumn(new object?[] { 3L }, "count", typeof(long));
        return builder.Build();
    }

    private static readonly IFlowNode Source = new DelegateNode(
        new("test.tableSource", 1, NodeCapability.Pure,
            Inputs: Array.Empty<PortSpec>(),
            Outputs: new PortSpec[] { new("out", PortType.Table) },
            Params: new ParamSpec[] { new("fail", ParamKind.Boolean, "false") },
            "A one-row table, or an error when fail=true."),
        (_, _, p) => p.GetBoolean("fail")
            ? throw new InvalidOperationException("source failed")
            : new FlowValue[] { new TableValue(SampleTable()) });

    private static readonly IFlowNode Consumer = new DelegateNode(
        new("test.consumer", 1, NodeCapability.Pure,
            Inputs: new PortSpec[] { new("table", PortType.Table, Optional: true) },
            Outputs: new PortSpec[] { new("out", PortType.Table) },
            Params: new ParamSpec[]
            {
                new("column", ParamKind.Text, Suggest: SuggestSource.ColumnsOf("table")),
                new("path", ParamKind.FilePath),
                new("table", ParamKind.Text, Suggest: SuggestSource.TablesInFile("path")),
            },
            "Passes its input through."),
        (_, inputs, _) => new[] { inputs[0] });

    private static readonly INodeRegistry Registry =
        new NodeRegistry(new[] { Source, Consumer });

    private static SuggestionList Resolve(EvalSnapshot snapshot, string nodeId,
        string param, FileTableProbe? probe = null)
    {
        var node = snapshot.Document.FindNode(nodeId)!;
        var spec = Registry.Find(node.Kind, node.Version)!.Spec;
        var suggest = spec.Params.First(p => p.Name == param).Suggest!;
        return SuggestEndpoints.Resolve(snapshot, Registry, nodeId, suggest, probe);
    }

    [Test]
    public void ColumnsOfInput_ReturnsUpstreamColumnsWithKinds()
    {
        var snapshot = Graph
            .Node("s", "test.tableSource")
            .Node("c", "test.consumer")
            .Connect("s.out", "c.table")
            .Build()
            .Evaluate(Registry);
        var list = Resolve(snapshot, "c", "column");
        Assert.That(list.Status, Is.EqualTo(SuggestStatus.Ok));
        Assert.That(list.Values.Select(v => v.Value), Is.EqualTo(new[] { "name", "count" }));
        Assert.That(list.Values.Select(v => v.Detail), Is.EqualTo(new[] { "Text", "Integer" }));
    }

    [Test]
    public void ColumnsOfInput_NoEdge_IsUnready()
    {
        var snapshot = Graph.Node("c", "test.consumer").Build().Evaluate(Registry);
        var list = Resolve(snapshot, "c", "column");
        Assert.That(list.Status, Is.EqualTo(SuggestStatus.Unready));
        Assert.That(list.Values, Is.Empty);
        Assert.That(list.Reason, Does.Contain("table"));
    }

    [Test]
    public void ColumnsOfInput_UpstreamError_IsUnavailable()
    {
        var snapshot = Graph
            .Node("s", "test.tableSource", ("fail", "true"))
            .Node("c", "test.consumer")
            .Connect("s.out", "c.table")
            .Build()
            .Evaluate(Registry);
        var list = Resolve(snapshot, "c", "column");
        Assert.That(list.Status, Is.EqualTo(SuggestStatus.Unavailable));
        Assert.That(list.Reason, Does.Contain("source failed"));
    }

    [Test]
    public void TablesInFile_EmptyPath_IsUnready()
    {
        var snapshot = Graph.Node("c", "test.consumer").Build().Evaluate(Registry);
        var list = Resolve(snapshot, "c", "table", _ => throw new InvalidOperationException("not called"));
        Assert.That(list.Status, Is.EqualTo(SuggestStatus.Unready));
        Assert.That(list.Reason, Does.Contain("path"));
    }

    [Test]
    public void TablesInFile_ProbeResult_IsOk()
    {
        var snapshot = Graph
            .Node("c", "test.consumer", ("path", "db.duckdb"))
            .Build()
            .Evaluate(Registry);
        var list = Resolve(snapshot, "c", "table",
            path => new[] { new Suggestion(path + ":a", null), new Suggestion(path + ":b", null) });
        Assert.That(list.Status, Is.EqualTo(SuggestStatus.Ok));
        Assert.That(list.Values.Select(v => v.Value),
            Is.EqualTo(new[] { "db.duckdb:a", "db.duckdb:b" }));
    }

    [Test]
    public void TablesInFile_ProbeThrows_IsUnavailable()
    {
        var snapshot = Graph
            .Node("c", "test.consumer", ("path", "missing.duckdb"))
            .Build()
            .Evaluate(Registry);
        var list = Resolve(snapshot, "c", "table",
            _ => throw new FileNotFoundException("no such file"));
        Assert.That(list.Status, Is.EqualTo(SuggestStatus.Unavailable));
        Assert.That(list.Reason, Does.Contain("no such file"));
    }

    [Test]
    public void TablesInFile_NoProbe_IsUnavailable()
    {
        var snapshot = Graph
            .Node("c", "test.consumer", ("path", "db.duckdb"))
            .Build()
            .Evaluate(Registry);
        Assert.That(Resolve(snapshot, "c", "table").Status, Is.EqualTo(SuggestStatus.Unavailable));
    }
}
