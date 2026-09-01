using Ara3D.BimOpenSchema.DuckDb;
using Ara3D.DataFlowEngine.Abstractions;

namespace BimOpenFlow.Nodes.TableOps;

/// <summary>Drops the named columns, keeping everything else — the complement of
/// table.project. Unknown names warn; dropping every column is an error.</summary>
public sealed class TableDropNode : IFlowNode
{
    public const string Kind = "table.drop";

    public NodeSpec Spec { get; } = new(
        Kind, 1, NodeCapability.Pure,
        Inputs: [new PortSpec("table", PortType.Table)],
        Outputs: [new PortSpec("table", PortType.Table)],
        Params: [new ParamSpec("columns", ParamKind.Text)],
        "Removes the named columns and keeps all others.");

    public IReadOnlyList<FlowValue> Eval(IEvalContext context,
        IReadOnlyList<FlowValue> inputs, ParamValues parameters)
    {
        var table = inputs.TableInput(0, Kind);
        var dropped = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var name in parameters.RequiredText("columns", Kind).SplitNames())
        {
            if (table.ColumnIndex(name) < 0)
                context.Warn($"{Kind}: no column named '{name}'.");
            else
                dropped.Add(table.CanonicalName(name, Kind));
        }
        if (dropped.Count == table.Columns.Count)
            throw new ArgumentException($"{Kind}: dropping every column leaves an empty table.");
        if (dropped.Count == 0)
            return [new TableValue(table)];
        var sql = $"SELECT * EXCLUDE ({string.Join(", ", dropped.Select(TableColumns.Ident))}) FROM t";
        return [new TableValue(TableColumns.RunSql(Kind, table, sql))];
    }
}
