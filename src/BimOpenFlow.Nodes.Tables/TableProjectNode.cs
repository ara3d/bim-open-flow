using Ara3D.DataFlowEngine.Abstractions;

namespace BimOpenFlow.Nodes.Tables;

/// <summary>Keeps the named columns in the given order; unknown names warn,
/// never error.</summary>
public sealed class TableProjectNode : IFlowNode
{
    public const string Kind = "table.project";

    public NodeSpec Spec { get; } = new(
        Kind, 1, NodeCapability.Pure,
        Inputs: [new PortSpec("table", PortType.Table)],
        Outputs: [new PortSpec("table", PortType.Table)],
        Params: [new ParamSpec("columns", ParamKind.Text)],
        "Keeps the comma-separated columns, in that order; unknown names warn.");

    public IReadOnlyList<FlowValue> Eval(IEvalContext context, IReadOnlyList<FlowValue> inputs, ParamValues parameters)
        => throw new NotImplementedException("track B");
}
