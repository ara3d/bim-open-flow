using Ara3D.BimOpenSchema.DuckDb;
using Ara3D.DataFlowEngine.Abstractions;
using Ara3D.DataTable;
using DuckDB.NET.Data;

namespace BimOpenFlow.Nodes.Effects;

/// <summary>sink.exportDuckDb: writes the input table into one table of a DuckDB
/// database file — the only node that ever opens a DuckDB file writable.</summary>
public sealed class ExportDuckDbNode : IFlowNode
{
    public const string Kind = "sink.exportDuckDb";

    public NodeSpec Spec { get; } = new(
        Kind, 1, NodeCapability.Effect,
        new[] { new PortSpec("in", PortType.Table) },
        new[] { new PortSpec("out", PortType.Table) },
        new[]
        {
            new ParamSpec("path", ParamKind.FilePath),
            new ParamSpec("table", ParamKind.Text),
            new ParamSpec("mode", ParamKind.Enum, "replace", new[] { "replace", "append", "failIfExists" }),
        },
        "Writes the input table into a DuckDB database file (booleans/integers as BIGINT, numbers as DOUBLE, text as VARCHAR). 'replace' drops and recreates the table, 'append' adds rows to a column-compatible table, 'failIfExists' refuses to touch an existing one. Outputs a one-row summary (path, rowCount, table).");

    public IReadOnlyList<FlowValue> Eval(IEvalContext context, IReadOnlyList<FlowValue> inputs, ParamValues parameters)
    {
        context.RequireRun(Kind);
        var table = inputs.TableAt(0);
        var path = parameters.RequiredPath("path");
        var tableName = parameters.RequiredText("table", Kind);
        var mode = parameters.GetEnum("mode", Kind, "replace", "replace", "append", "failIfExists");
        Write(table, Sinks.EnsureParentDir(path), tableName, mode);
        return new FlowValue[]
        {
            new TableValue(Sinks.SummaryRow("exportDuckDb",
                ("path", path),
                ("rowCount", (long)table.Rows.Count),
                ("table", tableName))),
        };
    }

    // The database may hold other tables, so the node edits the target in place
    // inside a transaction rather than temp-file-replacing the whole file.
    private static void Write(IDataTable table, string path, string tableName, string mode)
    {
        using var conn = BosDuckDb.Open(path);
        conn.Execute("BEGIN TRANSACTION");
        try
        {
            var exists = TableExists(conn, tableName);
            if (mode == "failIfExists" && exists)
                throw new InvalidOperationException($"{Kind}: table '{tableName}' already exists in '{path}'");
            if (mode == "append" && exists)
                ColumnSql.RequireCompatibleColumns(Kind, tableName, ExistingColumns(conn, tableName), ColumnSql.ColumnNames(table));
            if (mode == "replace")
                conn.Execute($"DROP TABLE IF EXISTS {ColumnSql.QuoteIdent(tableName)}");
            if (mode == "replace" || !exists)
                conn.Execute(CreateSql(table, tableName));
            InsertRows(conn, table, tableName);
            conn.Execute("COMMIT");
        }
        catch
        {
            conn.Execute("ROLLBACK");
            throw;
        }
    }

    private static string CreateSql(IDataTable table, string tableName)
    {
        var columns = table.Columns.Select(c =>
            $"{ColumnSql.QuoteIdent(c.Descriptor.Name)} {SqlType(ColumnSql.Classify(c.Descriptor.Type))}");
        return $"CREATE TABLE {ColumnSql.QuoteIdent(tableName)} ({string.Join(", ", columns)})";
    }

    private static string SqlType(ColumnKind kind)
        => kind switch
        {
            ColumnKind.Integer => "BIGINT",
            ColumnKind.Number => "DOUBLE",
            _ => "VARCHAR",
        };

    // TODO: row-at-a-time INSERT; switch to the DuckDB appender if large exports show up.
    private static void InsertRows(DuckDBConnection conn, IDataTable table, string tableName)
    {
        if (table.Columns.Count == 0)
            return;
        var kinds = ColumnSql.Kinds(table);
        var names = string.Join(", ", ColumnSql.ColumnNames(table).Select(ColumnSql.QuoteIdent));
        var placeholders = string.Join(", ", Enumerable.Range(0, kinds.Length).Select(_ => "?"));
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"INSERT INTO {ColumnSql.QuoteIdent(tableName)} ({names}) VALUES ({placeholders})";
        var parameters = new DuckDBParameter[kinds.Length];
        for (var i = 0; i < kinds.Length; i++)
            cmd.Parameters.Add(parameters[i] = new DuckDBParameter());
        for (var r = 0; r < table.Rows.Count; r++)
        {
            for (var c = 0; c < kinds.Length; c++)
                parameters[c].Value = ColumnSql.Normalize(table[c, r], kinds[c]) ?? DBNull.Value;
            cmd.ExecuteNonQuery();
        }
    }

    private static bool TableExists(DuckDBConnection conn, string tableName)
        => conn.ScalarInt64(
            "SELECT count(*) FROM information_schema.tables WHERE table_schema = 'main' AND table_name = "
            + DuckTableSql.QuoteLiteral(tableName)) > 0;

    private static IReadOnlyList<string> ExistingColumns(DuckDBConnection conn, string tableName)
    {
        var result = conn.Query(
            "SELECT column_name FROM information_schema.columns WHERE table_schema = 'main' AND table_name = "
            + DuckTableSql.QuoteLiteral(tableName) + " ORDER BY ordinal_position");
        var names = new List<string>();
        for (var r = 0; r < result.Rows.Count; r++)
            names.Add(result[0, r]?.ToString() ?? "");
        return names;
    }
}
