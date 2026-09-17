using Ara3D.DataFlowEngine.Abstractions;

namespace BimOpenFlow.Nodes.Tables;

/// <summary>The table-file and table-combinator pack: XLSX and SQLite readers
/// and catalogs, join, set operations, projection, and generators (inline,
/// range, calendar). BIM-free, DuckDB-free.</summary>
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
        new TableInlineNode(),
        new TableRangeNode(),
        new TableCalendarNode(),
    ];
}
