using Ara3D.BimOpenSchema.DuckDb;
using Ara3D.DataFlowEngine.Abstractions;
using Ara3D.DataTable;

namespace BimOpenFlow.Nodes.DuckDb;

/// <summary>Lists the tables of a .duckdb database file (read-only) with real
/// column and row counts. Discovery: what is in this database I was handed?</summary>
public sealed class DuckTablesNode : IFlowNode
{
    public const string Kind = "duck.tables";

    public NodeSpec Spec { get; } = new(
        Kind, 1, NodeCapability.Pure,
        Inputs: [],
        Outputs: [new PortSpec("tables", PortType.Table)],
        Params: [new ParamSpec("path", ParamKind.FilePath)],
        "Lists the tables in a .duckdb database file with their column and row counts.");

    public IReadOnlyList<FlowValue> Eval(IEvalContext context, IReadOnlyList<FlowValue> inputs, ParamValues parameters)
    {
        var path = parameters.RequiredText("path", Kind);
        if (!File.Exists(path))
            throw new FileNotFoundException($"{Kind}: file not found: {path}", path);
        using var conn = DuckDbOps.OpenReadOnly(path);
        var infos = conn.GetTableInfo();
        var builder = new DataTableBuilder("tables");
        builder.AddColumn(infos.Select(i => (object?)i.Table).ToArray(), "name", typeof(string));
        builder.AddColumn(infos.Select(i => (object?)(long)i.Columns.Count).ToArray(), "columnCount", typeof(long));
        builder.AddColumn(infos.Select(i => (object?)i.RowCount).ToArray(), "rowCount", typeof(long));
        return [new TableValue(builder.Build())];
    }
}
