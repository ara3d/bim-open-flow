using Ara3D.DataFlowEngine.Abstractions;

namespace BimOpenFlow.Nodes.TableOps;

/// <summary>The TableOps pack: filter, derive, aggregate, and sort, then rows, columns,
/// reshape, and window transforms, each a typed facade over one generated DuckDB clause
/// or one compiled expression.</summary>
public static class TableOpsNodes
{
    public static IReadOnlyList<IFlowNode> All { get; } =
    [
        new TableFilterNode(),
        new TableDeriveNode(),
        new TableAggregateNode(),
        new TableSortNode(),
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
