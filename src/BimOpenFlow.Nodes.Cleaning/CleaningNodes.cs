using Ara3D.DataFlowEngine.Abstractions;

namespace BimOpenFlow.Nodes.Cleaning;

/// <summary>The Cleaning pack: nulls, duplicates, text noise, and value
/// replacement, each a typed facade over one generated DuckDB clause.</summary>
public static class CleaningNodes
{
    public static IReadOnlyList<IFlowNode> All { get; } =
    [
        new TableFillNullsNode(),
        new TableDropNullsNode(),
        new TableDedupeNode(),
        new TableReplaceNode(),
    ];
}
