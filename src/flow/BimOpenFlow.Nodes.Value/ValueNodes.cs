using Ara3D.DataFlowEngine.Abstractions;

namespace BimOpenFlow.Nodes.Value;

/// <summary>The Value pack: constant nodes (text, number, integer, boolean, file path) whose
/// output feeds the bindable parameters of other nodes, so one value can be shared.</summary>
public static class ValueNodes
{
    public static IReadOnlyList<IFlowNode> All { get; } =
    [
        new TextNode(),
        new NumberNode(),
        new IntegerNode(),
        new BooleanNode(),
        new PathNode(),
    ];
}
