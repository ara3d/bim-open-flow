using Ara3D.DataFlowEngine;
using Ara3D.DataFlowEngine.Abstractions;
using Ara3D.DataFlowEngine.TestKit;
using BimOpenFlow.Contracts;
using NodeCapability = Ara3D.DataFlowEngine.Abstractions.NodeCapability;
using ParamKind = Ara3D.DataFlowEngine.Abstractions.ParamKind;
using PortType = Ara3D.DataFlowEngine.Abstractions.PortType;

namespace BimOpenFlow.Host.Api.Tests;

/// <summary>The TablesInFile suggestion reads the file of a wired Database source before the path parameter.</summary>
public sealed class SuggestWiredSourceTests
{
    private static readonly IFlowNode Source = new DelegateNode(
        new("test.dbSource", 1, NodeCapability.Pure,
            Inputs: Array.Empty<PortSpec>(),
            Outputs: new PortSpec[] { new("source", PortType.Database) },
            Params: new ParamSpec[] { new("fail", ParamKind.Boolean, "false") },
            "A database value, or an error when fail=true."),
        (_, _, p) => p.GetBoolean("fail")
            ? throw new InvalidOperationException("source failed")
            : new FlowValue[] { new DatabaseValue("wired.duckdb", "h") });

    private static readonly IFlowNode Table = new DelegateNode(
        new("test.dbTable", 1, NodeCapability.Pure,
            Inputs: new PortSpec[] { new("source", PortType.Database, Optional: true) },
            Outputs: Array.Empty<PortSpec>(),
            Params: new ParamSpec[]
            {
                new("path", ParamKind.FilePath),
                new("table", ParamKind.Text, Suggest: SuggestSource.TablesInFile("path")),
            },
            "Reads a table."),
        (_, _, _) => Array.Empty<FlowValue>());

    private static readonly INodeRegistry Registry = new NodeRegistry(new[] { Source, Table });

    private static SuggestionList Resolve(EvalSnapshot snapshot)
        => SuggestEndpoints.Resolve(snapshot, Registry, "t", SuggestSource.TablesInFile("path"),
            path => new[] { new Suggestion("tables of " + path, null) });

    [Test]
    public void WiredSource_OverridesThePathParameter()
    {
        var snapshot = Graph
            .Node("s", "test.dbSource")
            .Node("t", "test.dbTable", ("path", "own.duckdb"))
            .Connect("s.source", "t.source")
            .Build()
            .Evaluate(Registry);
        var list = Resolve(snapshot);
        Assert.That(list.Status, Is.EqualTo(SuggestStatus.Ok));
        Assert.That(list.Values.Single().Value, Is.EqualTo("tables of wired.duckdb"));
    }

    [Test]
    public void WiredSource_WorksWithNoPathSet()
    {
        var snapshot = Graph
            .Node("s", "test.dbSource")
            .Node("t", "test.dbTable")
            .Connect("s.source", "t.source")
            .Build()
            .Evaluate(Registry);
        Assert.That(Resolve(snapshot).Status, Is.EqualTo(SuggestStatus.Ok));
    }

    [Test]
    public void WiredSourceThatFailed_IsUnavailableWithItsError()
    {
        var snapshot = Graph
            .Node("s", "test.dbSource", ("fail", "true"))
            .Node("t", "test.dbTable", ("path", "own.duckdb"))
            .Connect("s.source", "t.source")
            .Build()
            .Evaluate(Registry);
        var list = Resolve(snapshot);
        Assert.That(list.Status, Is.EqualTo(SuggestStatus.Unavailable));
        Assert.That(list.Reason, Does.Contain("source failed"));
    }

    [Test]
    public void NoWire_UsesThePathParameter()
    {
        var snapshot = Graph.Node("t", "test.dbTable", ("path", "own.duckdb")).Build().Evaluate(Registry);
        Assert.That(Resolve(snapshot).Values.Single().Value, Is.EqualTo("tables of own.duckdb"));
    }
}
