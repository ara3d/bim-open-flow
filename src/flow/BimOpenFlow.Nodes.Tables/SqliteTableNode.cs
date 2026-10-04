using Ara3D.DataFlowEngine.Abstractions;
using Ara3D.DataTable;
using Microsoft.Data.Sqlite;

namespace BimOpenFlow.Nodes.Tables;

/// <summary>Reads one whole SQLite table by name — the no-SQL companion to
/// sqlite.query, with the same column-type unification.</summary>
public sealed class SqliteTableNode : IFlowNode
{
    public const string Kind = "sqlite.table";

    public NodeSpec Spec { get; } = new(
        Kind, 1, NodeCapability.Pure,
        Inputs: [],
        Outputs: [new PortSpec("table", PortType.Table)],
        Params:
        [
            new ParamSpec("path", ParamKind.FilePath),
            new ParamSpec("table", ParamKind.Text),
        ],
        "Reads one whole table from a SQLite database file (SELECT *, read-only).");

    public IReadOnlyList<FlowValue> Eval(IEvalContext context, IReadOnlyList<FlowValue> inputs, ParamValues parameters)
    {
        var path = parameters.RequiredText("path", Kind);
        if (!File.Exists(path))
            throw new FileNotFoundException($"{Kind}: file not found: {path}", path);
        var table = parameters.RequiredText("table", Kind);
        try
        {
            using var connection = SqliteOps.OpenReadOnly(path);
            var name = SqliteOps.TableNames(connection)
                    .FirstOrDefault(n => string.Equals(n, table, StringComparison.OrdinalIgnoreCase))
                ?? throw new ArgumentException($"{Kind}: no table named '{table}'.");
            return [new TableValue(SqliteOps.QueryTable(connection,
                $"SELECT * FROM {SqliteOps.QuoteIdentifier(name)}", name))];
        }
        catch (SqliteException e)
        {
            throw new ArgumentException($"{Kind}: {e.Message}", e);
        }
    }
}
