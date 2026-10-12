using Ara3D.DataFlowEngine.Abstractions;

namespace BimOpenFlow.Nodes.Tables;

/// <summary>The columns of a SQLite table or query, from a statement that returns no rows. SQLite types
/// each value rather than each column, so the names are known and the types are Unknown.</summary>
internal static class SqliteSchemas
{
    public static SchemaResult Of(string path, IReadOnlyDictionary<string, string> options)
    {
        using var connection = SqliteOps.OpenReadOnly(path);
        if (options.TryGetValue("sql", out var sql) && sql.Length > 0)
            return Names(connection, sql.TrimEnd().TrimEnd(';'));
        var table = options.GetValueOrDefault("table", "");
        var name = SqliteOps.TableNames(connection).FirstOrDefault(n => string.Equals(n, table, StringComparison.OrdinalIgnoreCase));
        return name is null
            ? SchemaResult.Fail($"no table named '{table}' in {path}.")
            : Names(connection, $"SELECT * FROM {SqliteOps.QuoteIdentifier(name)}");
    }

    private static SchemaResult Names(Microsoft.Data.Sqlite.SqliteConnection connection, string sql)
    {
        var names = SqliteOps.ColumnNames(connection, $"SELECT * FROM ({sql}) LIMIT 0");
        return SchemaResult.Of(new Schema(names.Select(n => new Column(n, ColumnType.Unknown)).ToList()));
    }
}
