using Ara3D.DataFlowEngine.Abstractions;

namespace BimOpenFlow.Nodes.Tables;

/// <summary>Row-set algebra on a key column: a's columns and row order pass
/// through; union appends b rows whose key is absent from a.</summary>
public sealed class TableSetOpNode : IFlowNode
{
    public const string Kind = "table.setOp";

    public NodeSpec Spec { get; } = new(
        Kind, 1, NodeCapability.Pure,
        Inputs:
        [
            new PortSpec("a", PortType.Table),
            new PortSpec("b", PortType.Table),
        ],
        Outputs: [new PortSpec("table", PortType.Table)],
        Params:
        [
            new ParamSpec("op", ParamKind.Enum, "intersect", ["union", "intersect", "subtract"]),
            new ParamSpec("key", ParamKind.Text),
        ],
        "Keeps a's rows by key-set operation with b: union, intersect, or subtract.");

    public IReadOnlyList<FlowValue> Eval(IEvalContext context, IReadOnlyList<FlowValue> inputs, ParamValues parameters)
        => throw new NotImplementedException("track B");
}
