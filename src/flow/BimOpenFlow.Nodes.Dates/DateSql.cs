using Ara3D.BimOpenSchema.DuckDb;
using Ara3D.DataTable;

namespace BimOpenFlow.Nodes.Dates;

/// <summary>SQL fragments shared by the Dates nodes: order-preserving query
/// wrappers, ISO date column validation, and timestamp/text conversions.</summary>
internal static class DateSql
{
    /// <summary>An ordinal column name not colliding with any input column.</summary>
    public static string OrdinalName(this IDataTable table)
        => TableColumns.FreeName("__ord", table);

    public static void RequireColumn(this IDataTable table, string column, string kind)
    {
        if (!table.Columns.Any(c => c.Descriptor.Name == column))
            throw new ArgumentException($"{kind}: no column named '{column}'.");
    }

    public static void RequireNewColumn(this IDataTable table, string name, string kind)
    {
        if (table.Columns.Any(c => c.Descriptor.Name == name))
            throw new ArgumentException($"{kind}: column '{name}' already exists.");
    }

    /// <summary>The column's value cast to TIMESTAMP (via VARCHAR, so any
    /// stored kind reaches the same cast path).</summary>
    public static string TsExpr(string column)
        => $"CAST(CAST({DuckTableSql.QuoteIdent(column)} AS VARCHAR) AS TIMESTAMP)";

    public static string TryTsExpr(string column)
        => $"TRY_CAST(CAST({DuckTableSql.QuoteIdent(column)} AS VARCHAR) AS TIMESTAMP)";

    /// <summary>Renders a TIMESTAMP expression back to canonical wire text:
    /// "yyyy-MM-dd" at midnight, "yyyy-MM-ddTHH:mm:ss" otherwise.</summary>
    public static string IsoTextExpr(string ts)
        => $"CASE WHEN strftime({ts}, '%H:%M:%S') = '00:00:00'"
           + $" THEN strftime({ts}, '%Y-%m-%d')"
           + $" ELSE strftime({ts}, '%Y-%m-%dT%H:%M:%S') END";

    /// <summary>Errors unless every non-null value in the column is ISO-8601
    /// date/datetime text, pointing the user at date.parse.</summary>
    public static void RequireIsoDates(this IDataTable table, string column, string kind)
    {
        var c = DuckTableSql.QuoteIdent(column);
        var bad = DuckTableSql.Run(table,
            $"SELECT count(*) FROM t WHERE {c} IS NOT NULL AND {TryTsExpr(column)} IS NULL");
        var count = Convert.ToInt64(bad[0, 0]);
        if (count > 0)
            throw new ArgumentException(
                $"{kind}: column '{column}' has {count} value(s) that are not ISO-8601 date/datetime text; run date.parse first.");
    }

    /// <summary>Runs a projection over the table with the input row order
    /// preserved via a temporary ordinal column excluded from the output. The
    /// ordinal is materialized in C# (never row_number() over an unordered
    /// scan, whose order DuckDB does not guarantee under parallel execution).
    /// 'projection' follows "SELECT * EXCLUDE (ord)" (e.g. " REPLACE (...)" or
    /// ", expr AS name"); 'where' is a full WHERE clause or empty. Engine
    /// failures are rethrown with the node kind prefixed.</summary>
    public static IDataTable RunOrdered(IDataTable table, string projection, string where, string kind)
    {
        var ordName = table.OrdinalName();
        var ord = DuckTableSql.QuoteIdent(ordName);
        return DuckTableSql.Run(kind, table.WithOrdinal(ordName),
            $"SELECT * EXCLUDE ({ord}){projection} FROM t {where} ORDER BY {ord}");
    }

    /// <summary>The in-place-unless-named convention: empty name replaces
    /// 'column' with 'expr'; a non-empty name appends a new column (error when
    /// it already exists).</summary>
    public static IDataTable RunColumnExpr(IDataTable table, string column, string name,
        string expr, string kind)
    {
        if (string.IsNullOrEmpty(name))
            return RunOrdered(table, $" REPLACE (({expr}) AS {DuckTableSql.QuoteIdent(column)})", "", kind);
        table.RequireNewColumn(name, kind);
        return RunOrdered(table, $", ({expr}) AS {DuckTableSql.QuoteIdent(name)}", "", kind);
    }
}
