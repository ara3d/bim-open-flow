using Ara3D.DataFlowEngine.Abstractions;
using Ara3D.DataTable;
using Microsoft.Data.Sqlite;

namespace BimOpenFlow.Nodes.Effects;

/// <summary>sink.exportSqlite: writes the input table into one table of a SQLite
/// database file, inside a single transaction.</summary>
public sealed class ExportSqliteNode : IFlowNode
{
    public const string Kind = "sink.exportSqlite";

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
        "Writes the input table into a SQLite database (booleans/integers as INTEGER, numbers as REAL, text as TEXT). 'replace' drops and recreates the table, 'append' adds rows to a column-compatible table, 'failIfExists' refuses to touch an existing one. Outputs a one-row summary (path, rowCount, table).");

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
            new TableValue(Sinks.SummaryRow("exportSqlite",
                ("path", path),
                ("rowCount", (long)table.Rows.Count),
                ("table", tableName))),
        };
    }

    private static void Write(IDataTable table, string path, string tableName, string mode)
    {
        using var conn = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Pooling = false,
        }.ToString());
        conn.Open();
        using var tx = conn.BeginTransaction();
        var exists = TableExists(conn, tx, tableName);
        if (mode == "failIfExists" && exists)
            throw new InvalidOperationException($"{Kind}: table '{tableName}' already exists in '{path}'");
        if (mode == "append" && exists)
            ColumnSql.RequireCompatibleColumns(Kind, tableName, ExistingColumns(conn, tx, tableName), ColumnSql.ColumnNames(table));
        if (mode == "replace")
            Execute(conn, tx, $"DROP TABLE IF EXISTS {ColumnSql.QuoteIdent(tableName)}");
        if (mode == "replace" || !exists)
            Execute(conn, tx, CreateSql(table, tableName));
        InsertRows(conn, tx, table, tableName);
        tx.Commit();
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
            ColumnKind.Integer => "INTEGER",
            ColumnKind.Number => "REAL",
            _ => "TEXT",
        };

    private static void InsertRows(SqliteConnection conn, SqliteTransaction tx, IDataTable table, string tableName)
    {
        if (table.Columns.Count == 0)
            return;
        var kinds = ColumnSql.Kinds(table);
        var names = string.Join(", ", ColumnSql.ColumnNames(table).Select(ColumnSql.QuoteIdent));
        var placeholders = string.Join(", ", Enumerable.Range(0, kinds.Length).Select(i => $"@p{i}"));
        using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = $"INSERT INTO {ColumnSql.QuoteIdent(tableName)} ({names}) VALUES ({placeholders})";
        var parameters = new SqliteParameter[kinds.Length];
        for (var i = 0; i < kinds.Length; i++)
            cmd.Parameters.Add(parameters[i] = new SqliteParameter($"@p{i}", null));
        for (var r = 0; r < table.Rows.Count; r++)
        {
            for (var c = 0; c < kinds.Length; c++)
                parameters[c].Value = ColumnSql.Normalize(table[c, r], kinds[c]) ?? DBNull.Value;
            cmd.ExecuteNonQuery();
        }
    }

    private static bool TableExists(SqliteConnection conn, SqliteTransaction tx, string tableName)
    {
        using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = "SELECT count(*) FROM sqlite_master WHERE type = 'table' AND name = @name";
        cmd.Parameters.AddWithValue("@name", tableName);
        return Convert.ToInt64(cmd.ExecuteScalar()) > 0;
    }

    private static IReadOnlyList<string> ExistingColumns(SqliteConnection conn, SqliteTransaction tx, string tableName)
    {
        using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = "SELECT name FROM pragma_table_info(@name) ORDER BY cid";
        cmd.Parameters.AddWithValue("@name", tableName);
        using var reader = cmd.ExecuteReader();
        var names = new List<string>();
        while (reader.Read())
            names.Add(reader.GetString(0));
        return names;
    }

    private static void Execute(SqliteConnection conn, SqliteTransaction tx, string sql)
    {
        using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }
}
