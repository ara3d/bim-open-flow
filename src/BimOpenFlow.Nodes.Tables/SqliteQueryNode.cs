using Ara3D.DataFlowEngine.Abstractions;
using Ara3D.DataTable;
using Microsoft.Data.Sqlite;

namespace BimOpenFlow.Nodes.Tables;

/// <summary>Opens a SQLite database file read-only and runs one validated
/// SELECT/WITH query against it.</summary>
public sealed class SqliteQueryNode : IFlowNode
{
    public const string Kind = "sqlite.query";

    public NodeSpec Spec { get; } = new(
        Kind, 1, NodeCapability.Pure,
        Inputs: [],
        Outputs: [new PortSpec("table", PortType.Table)],
        Params:
        [
            new ParamSpec("path", ParamKind.FilePath),
            new ParamSpec("sql", ParamKind.Text),
        ],
        "Runs one read-only SQL query against a SQLite database file.");

    public IReadOnlyList<FlowValue> Eval(IEvalContext context, IReadOnlyList<FlowValue> inputs, ParamValues parameters)
    {
        var path = parameters.RequiredText("path", Kind);
        if (!File.Exists(path))
            throw new FileNotFoundException($"{Kind}: file not found: {path}", path);
        var sql = ValidateSelect(parameters.RequiredText("sql", Kind));
        try
        {
            return [new TableValue(Query(path, sql))];
        }
        catch (SqliteException e)
        {
            throw new ArgumentException($"{Kind}: {e.Message}", e);
        }
    }

    /// <summary>A single SELECT/WITH statement: nothing but whitespace may follow a semicolon.</summary>
    private static string ValidateSelect(string sql)
    {
        var trimmed = sql.Trim();
        if (!trimmed.StartsWith("SELECT", StringComparison.OrdinalIgnoreCase)
            && !trimmed.StartsWith("WITH", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException($"{Kind}: 'sql' must be a single SELECT or WITH statement.");
        var semi = trimmed.IndexOf(';');
        if (semi >= 0 && !string.IsNullOrWhiteSpace(trimmed[(semi + 1)..]))
            throw new ArgumentException($"{Kind}: 'sql' must be a single statement.");
        return trimmed;
    }

    private static IDataTable Query(string path, string sql)
    {
        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Mode = SqliteOpenMode.ReadOnly,
        }.ToString();
        using var connection = new SqliteConnection(connectionString);
        connection.Open();
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

        var builder = new DataTableBuilder("query");
        for (var i = 0; i < names.Length; i++)
            builder.AddColumn(Unify(cells[i], out var type), names[i], type);
        return builder.Build();
    }

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
