using Ara3D.DataTable;
using Microsoft.Data.Sqlite;

namespace BimOpenFlow.Nodes.Tables;

/// <summary>Shared read-only SQLite access: connection opening, query
/// materialization with per-column type unification, and catalog helpers.</summary>
internal static class SqliteOps
{
    public static SqliteConnection OpenReadOnly(string path)
    {
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Mode = SqliteOpenMode.ReadOnly,
        }.ToString());
        connection.Open();
        return connection;
    }

    public static IDataTable QueryTable(SqliteConnection connection, string sql, string name)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        using var reader = command.ExecuteReader();

        var names = new string[reader.FieldCount];
        var cells = new List<object?>[reader.FieldCount];
        for (var i = 0; i < reader.FieldCount; i++)
        {
            names[i] = reader.GetName(i);
            cells[i] = [];
        }
        while (reader.Read())
            for (var i = 0; i < reader.FieldCount; i++)
                cells[i].Add(reader.IsDBNull(i) ? null : reader.GetValue(i));

        var builder = new DataTableBuilder(name);
        for (var i = 0; i < names.Length; i++)
            builder.AddColumn(Unify(cells[i], out var type), names[i], type);
        return builder.Build();
    }

    public static long QueryScalar(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        return Convert.ToInt64(command.ExecuteScalar());
    }

    /// <summary>User table names (views and sqlite_ internals excluded), in name order.</summary>
    public static IReadOnlyList<string> TableNames(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText =
            "SELECT name FROM sqlite_master WHERE type = 'table' AND name NOT LIKE 'sqlite_%' ORDER BY name";
        using var reader = command.ExecuteReader();
        var names = new List<string>();
        while (reader.Read())
            names.Add(reader.GetString(0));
        return names;
    }

    public static string QuoteIdentifier(string name)
        => "\"" + name.Replace("\"", "\"\"") + "\"";

    public static string QuoteLiteral(string text)
        => "'" + text.Replace("'", "''") + "'";

    /// <summary>SQLite columns are dynamically typed per row: one non-null CLR type wins,
    /// long+double widens to double, anything else lands as canonical text.</summary>
    private static object?[] Unify(List<object?> cells, out Type type)
    {
        var types = cells.Where(c => c != null).Select(c => c!.GetType()).Distinct().ToList();
        type = types switch
        {
            { Count: 0 } => typeof(string),
            { Count: 1 } => types[0],
            _ when types.All(t => t == typeof(long) || t == typeof(double)) => typeof(double),
            _ => typeof(string),
        };
        var target = type;
        return type switch
        {
            _ when types.Count <= 1 => cells.ToArray(),
            _ when target == typeof(double) => cells.Select(c => c == null ? null : (object?)Convert.ToDouble(c)).ToArray(),
            _ => cells.Select(c => (object?)TableOps.CanonicalText(c)).ToArray(),
        };
    }
}
