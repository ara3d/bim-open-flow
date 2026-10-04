using Ara3D.DataFlowEngine.Abstractions;
using Ara3D.DataTable;
using Microsoft.Data.Sqlite;

namespace BimOpenFlow.Nodes.Tables;

/// <summary>Lists the user tables of a SQLite database with column and row
/// counts — discovery before naming a table in sqlite.table.</summary>
public sealed class SqliteTablesNode : IFlowNode
{
    public const string Kind = "sqlite.tables";

    public NodeSpec Spec { get; } = new(
        Kind, 1, NodeCapability.Pure,
        Inputs: [],
        Outputs: [new PortSpec("tables", PortType.Table)],
        Params: [new ParamSpec("path", ParamKind.FilePath)],
        "Lists the tables in a SQLite database file: name, columnCount, rowCount (read-only).");

    public IReadOnlyList<FlowValue> Eval(IEvalContext context, IReadOnlyList<FlowValue> inputs, ParamValues parameters)
    {
        var path = parameters.RequiredText("path", Kind);
        if (!File.Exists(path))
            throw new FileNotFoundException($"{Kind}: file not found: {path}", path);
        try
        {
            return [new TableValue(Load(path))];
        }
        catch (SqliteException e)
        {
            throw new ArgumentException($"{Kind}: {e.Message}", e);
        }
    }

    private static IDataTable Load(string path)
    {
        using var connection = SqliteOps.OpenReadOnly(path);
        var tableNames = SqliteOps.TableNames(connection);
        var names = new object?[tableNames.Count];
        var columnCounts = new object?[tableNames.Count];
        var rowCounts = new object?[tableNames.Count];
        for (var i = 0; i < tableNames.Count; i++)
        {
            names[i] = tableNames[i];
            columnCounts[i] = SqliteOps.QueryScalar(connection,
                $"SELECT COUNT(*) FROM pragma_table_info({SqliteOps.QuoteLiteral(tableNames[i])})");
            rowCounts[i] = SqliteOps.QueryScalar(connection,
                $"SELECT COUNT(*) FROM {SqliteOps.QuoteIdentifier(tableNames[i])}");
        }
        var builder = new DataTableBuilder("tables");
        builder.AddColumn(names, "name", typeof(string));
        builder.AddColumn(columnCounts, "columnCount", typeof(long));
        builder.AddColumn(rowCounts, "rowCount", typeof(long));
        return builder.Build();
    }
}
