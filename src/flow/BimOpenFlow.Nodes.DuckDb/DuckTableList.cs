using Ara3D.BimOpenSchema.DuckDb;
using Ara3D.DataTable;
using DuckDB.NET.Data;

namespace BimOpenFlow.Nodes.DuckDb;

/// <summary>The table list of a database: one row per table with its column and row counts. It is
/// what duck.tables outputs and what a pane shows for a Database value.</summary>
public static class DuckTableList
{
    public static IDataTable Of(DuckDBConnection conn)
    {
        var infos = conn.GetTableInfo();
        var builder = new DataTableBuilder("tables");
        builder.AddColumn(infos.Select(i => (object?)i.Table).ToArray(), "name", typeof(string));
        builder.AddColumn(infos.Select(i => (object?)(long)i.Columns.Count).ToArray(), "columnCount", typeof(long));
        builder.AddColumn(infos.Select(i => (object?)i.RowCount).ToArray(), "rowCount", typeof(long));
        return builder.Build();
    }
}
