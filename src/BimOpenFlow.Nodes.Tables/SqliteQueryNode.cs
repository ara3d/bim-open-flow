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
        using var connection = SqliteOps.OpenReadOnly(path);
        return SqliteOps.QueryTable(connection, sql, "query");
    }
}
