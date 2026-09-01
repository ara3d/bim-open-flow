using Ara3D.DataFlowEngine.Abstractions;

namespace BimOpenFlow.Nodes.Tables;

/// <summary>Joins table b's columns onto table a by key column. Unmatched and
/// duplicate-key counts surface as warnings, never silently.</summary>
public sealed class TableJoinNode : IFlowNode
{
    public const string Kind = "table.join";

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
            new ParamSpec("aKey", ParamKind.Text),
            new ParamSpec("bKey", ParamKind.Text, ""),
            new ParamSpec("mode", ParamKind.Enum, "left", ["left", "inner"]),
        ],
        "Joins b's columns onto a by key (bKey defaults to aKey); left keeps all a rows, inner keeps matches.");

    public IReadOnlyList<FlowValue> Eval(IEvalContext context, IReadOnlyList<FlowValue> inputs, ParamValues parameters)
        => throw new NotImplementedException("track B");
}
