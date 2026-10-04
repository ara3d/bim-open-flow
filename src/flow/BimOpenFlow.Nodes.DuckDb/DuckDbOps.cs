using System.Globalization;
using Ara3D.BimOpenSchema.DuckDb;
using Ara3D.DataFlowEngine.Abstractions;
using Ara3D.DataTable;
using DuckDB.NET.Data;

namespace BimOpenFlow.Nodes.DuckDb;

/// <summary>Shared helpers for the DuckDB pack: parameter extraction, read-only
/// connections, and SQL-literal escaping. Duplicates the tiny NodeArgs helpers
/// from the Bos pack because node packs do not reference each other.</summary>
public static class DuckDbOps
{
    /// <summary>Validates the 'sql' parameter as a single SELECT/WITH statement,
    /// prefixing any rejection with the node kind.</summary>
    public static string ReadOnlySql(this ParamValues parameters, string kind)
    {
        var sql = parameters.RequiredText("sql", kind);
        try
        {
            return BosDuckDbQueries.ReadOnlyQuery(sql);
        }
        catch (ArgumentException e)
        {
            throw new ArgumentException($"{kind}: {e.Message}", e);
        }
    }

    /// <summary>Opens an existing DuckDB database file without write access, so a
    /// query node can never mutate the file.</summary>
    public static DuckDBConnection OpenReadOnly(string path)
    {
        var conn = new DuckDBConnection($"DataSource={path};ACCESS_MODE=READ_ONLY");
        conn.Open();
        return conn;
    }

    /// <summary>Escapes a file path for use inside a single-quoted SQL literal.</summary>
    public static string ToSqlLiteral(this string path)
        => path.Replace('\\', '/').Replace("'", "''");

    /// <summary>Returns a copy of the table with each column name mapped through
    /// <paramref name="rename"/>; names the function leaves unchanged pass through.</summary>
    public static IDataTable RenameColumns(this IDataTable table, Func<string, string> rename)
    {
        var builder = new DataTableBuilder(table.Name);
        foreach (var column in table.Columns)
        {
            var cells = new object?[table.Rows.Count];
            for (var row = 0; row < cells.Length; row++)
                cells[row] = table[column.ColumnIndex, row];
            builder.AddColumn(cells, rename(column.Descriptor.Name), column.Descriptor.Type);
        }
        return builder.Build();
    }

}
