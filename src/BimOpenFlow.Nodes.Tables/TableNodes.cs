using Ara3D.DataFlowEngine.Abstractions;

namespace BimOpenFlow.Nodes.Tables;

/// <summary>The table-file and table-combinator pack: XLSX and SQLite readers,
/// join, set operations, and projection. BIM-free, DuckDB-free.</summary>
public static class TableNodes
{
    public static IReadOnlyList<IFlowNode> All { get; } =
    [
        new XlsxReadNode(),
        new XlsxSheetsNode(),
        new SqliteQueryNode(),
        new SqliteTableNode(),
        new SqliteTablesNode(),
        new TableJoinNode(),
        new TableSetOpNode(),
        new TableProjectNode(),
    ];
}
