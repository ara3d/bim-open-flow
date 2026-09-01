using Ara3D.DataFlowEngine.Abstractions;

namespace BimOpenFlow.Nodes.TableOps;

/// <summary>The TableOps pack: rows, columns, reshape, and window transforms,
/// each a typed facade over one generated DuckDB clause.</summary>
public static class TableOpsNodes
{
    public static IReadOnlyList<IFlowNode> All { get; } =
    [
        new TableLimitNode(),
    ];
}
