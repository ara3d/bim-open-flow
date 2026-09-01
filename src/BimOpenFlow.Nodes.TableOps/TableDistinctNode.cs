using Ara3D.BimOpenSchema.DuckDb;
using Ara3D.DataFlowEngine.Abstractions;

namespace BimOpenFlow.Nodes.TableOps;

/// <summary>Whole-row distinct, or first-row-per-key when key columns are named.
/// Row order follows the first occurrence in the input.</summary>
public sealed class TableDistinctNode : IFlowNode
{
    public const string Kind = "table.distinct";

    public NodeSpec Spec { get; } = new(
        Kind, 1, NodeCapability.Pure,
        Inputs: [new PortSpec("table", PortType.Table)],
        Outputs: [new PortSpec("table", PortType.Table)],
        Params: [new ParamSpec("columns", ParamKind.Text)],
        "Removes duplicate rows; with key columns named, keeps the first row per key with all columns.");

    public IReadOnlyList<FlowValue> Eval(IEvalContext context,
        IReadOnlyList<FlowValue> inputs, ParamValues parameters)
    {
        var table = inputs.TableInput(0, Kind);
        var keys = parameters.GetText("columns").SplitNames()
            .Select(n => table.CanonicalName(n, Kind)).ToList();
        var partition = (keys.Count > 0 ? keys : table.Names()).Select(TableColumns.Ident);
        var ord = TableColumns.FreeName("__row__", table);
        var rn = TableColumns.FreeName("__rn__", table);
        var cols = string.Join(", ", table.Names().Select(TableColumns.Ident));
        var sql = $"""
            SELECT {cols} FROM (
              SELECT *, row_number() OVER (PARTITION BY {string.Join(", ", partition)} ORDER BY {ord.Ident()}) AS {rn.Ident()}
              FROM t)
            WHERE {rn.Ident()} = 1 ORDER BY {ord.Ident()}
            """;
        return [new TableValue(DuckTableSql.Run(table.WithOrdinal(ord), sql))];
    }
}
