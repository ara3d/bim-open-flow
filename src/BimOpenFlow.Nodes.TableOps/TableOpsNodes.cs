using Ara3D.DataFlowEngine.Abstractions;

namespace BimOpenFlow.Nodes.TableOps;

/// <summary>The TableOps pack: rows, columns, reshape, and window transforms,
/// each a typed facade over one generated DuckDB clause.</summary>
public static class TableOpsNodes
{
    public static IReadOnlyList<IFlowNode> All { get; } =
    [
        new TableCastNode(),
        new TableConcatNode(),
        new TableDistinctNode(),
        new TableDropNode(),
        new TableLimitNode(),
        new TablePivotNode(),
        new TableProfileNode(),
        new TableRenameNode(),
        new TableSampleNode(),
        new TableSchemaNode(),
        new TableSplitColumnNode(),
        new TableTransposeNode(),
        new TableUnpivotNode(),
        new TableWindowNode(),
    ];
}
