using Ara3D.DataFlowEngine.TestKit;
using Ara3D.DataTable;

namespace BimOpenFlow.GraphText.Tests;

/// <summary>Hand-built nodes and helpers for the printer's unit tests.</summary>
public static class Fixtures
{
    /// <summary>A source node of the given kind that emits the values on ports out0, out1, ...</summary>
    public static IFlowNode Source(string kind, params FlowValue[] values)
        => new DelegateNode(
            new(kind, 1, NodeCapability.Pure, [],
                values.Select((v, i) => new PortSpec($"out{i}", PortType.Any)).ToList(), []),
            (_, _, _) => values);

    /// <summary>A one-input node of the given kind that emits the values, ignoring its input.</summary>
    public static IFlowNode Transform(string kind, params FlowValue[] values)
        => new DelegateNode(
            new(kind, 1, NodeCapability.Pure, [new("in", PortType.Any)],
                values.Select((v, i) => new PortSpec($"out{i}", PortType.Any)).ToList(), []),
            (_, _, _) => values);

    public static INodeRegistry Registry(params IFlowNode[] nodes)
        => new NodeRegistry([.. TestNodes.All, .. nodes]);

    public static string Print(GraphDocument doc, INodeRegistry registry, GraphTextOptions? options = null)
        => GraphText.Print(doc, doc.Evaluate(registry), registry, options);

    /// <summary>The printed text of a graph with one source node "s" emitting the values.</summary>
    public static string PrintSource(GraphTextOptions? options, params FlowValue[] values)
        => Print(Graph.Node("s", "fixture.source").Build(), Registry(Source("fixture.source", values)), options);

    public static TableValue Table(params (string Name, Type Type, object?[] Cells)[] columns)
        => NodeTestHelpers.Table(columns);

    public static IDataTable Rows(this FlowValue value)
        => ((TableValue)value).Table;
}
