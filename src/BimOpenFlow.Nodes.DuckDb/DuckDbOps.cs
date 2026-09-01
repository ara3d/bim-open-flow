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
    public static string RequiredText(this ParamValues parameters, string name, string kind)
    {
        var text = parameters.GetText(name);
        return !string.IsNullOrWhiteSpace(text)
            ? text
            : throw new ArgumentException($"{kind}: parameter '{name}' is required.");
    }

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

    /// <summary>
    /// Tables on the wire carry only the five spec value kinds, so DuckDB
    /// DATE/TIME/TIMESTAMP columns become ISO-8601 text (matching xlsx.read).
    /// Returns the input table unchanged when no such column exists.
    /// </summary>
    public static IDataTable NormalizeDates(this IDataTable table)
    {
        if (!table.Columns.Any(c => IsDateLike(c.Descriptor.Type)))
            return table;
        var builder = new DataTableBuilder(table.Name);
        foreach (var column in table.Columns)
        {
            var dateLike = IsDateLike(column.Descriptor.Type);
            var cells = new object?[table.Rows.Count];
            for (var row = 0; row < cells.Length; row++)
            {
                var cell = table[column.ColumnIndex, row];
                cells[row] = dateLike ? IsoText(cell) : cell;
            }
            builder.AddColumn(cells, column.Descriptor.Name,
                dateLike ? typeof(string) : column.Descriptor.Type);
        }
        return builder.Build();
    }

    private static bool IsDateLike(Type type)
        => type == typeof(DateOnly) || type == typeof(DateTime)
           || type == typeof(TimeOnly) || type == typeof(DateTimeOffset);

    private static string? IsoText(object? cell)
        => cell switch
        {
            null => null,
            DateOnly d => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            DateTime d => d.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture),
            TimeOnly t => t.ToString("HH:mm:ss", CultureInfo.InvariantCulture),
            DateTimeOffset d => d.ToString("yyyy-MM-ddTHH:mm:sszzz", CultureInfo.InvariantCulture),
            _ => cell.ToString(),
        };
}
