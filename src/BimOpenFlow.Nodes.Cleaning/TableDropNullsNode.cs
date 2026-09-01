using Ara3D.BimOpenSchema.DuckDb;
using Ara3D.DataFlowEngine.Abstractions;

namespace BimOpenFlow.Nodes.Cleaning;

/// <summary>Drops rows where any (or all) of the listed columns are null,
/// preserving row order and warning with the dropped-row count.</summary>
public sealed class TableDropNullsNode : IFlowNode
{
    public const string Kind = "table.dropNulls";

    public NodeSpec Spec { get; } = new(
        Kind, 1, NodeCapability.Pure,
        Inputs: [new PortSpec("table", PortType.Table)],
        Outputs: [new PortSpec("table", PortType.Table)],
        Params:
        [
            new ParamSpec("columns", ParamKind.Text),
            new ParamSpec("mode", ParamKind.Enum, "any", ["any", "all"]),
        ],
        "Drops rows where any/all of 'columns' (empty = all columns) are null; warns with the dropped count.");

    public IReadOnlyList<FlowValue> Eval(IEvalContext context,
        IReadOnlyList<FlowValue> inputs, ParamValues parameters)
    {
        var table = inputs.TableInput(0, Kind);
        var names = parameters.GetText("columns").SplitNames()
            .Select(n => table.RequireColumn(n, Kind).Descriptor.Name).ToList();
        if (names.Count == 0)
            names = table.Columns.Select(c => c.Descriptor.Name).ToList();
        var mode = parameters.RequiredEnum("mode", Kind, "any", "any", "all");
        var joiner = mode == "any" ? " AND " : " OR ";
        var keep = string.Join(joiner,
            names.Select(n => $"{DuckTableSql.QuoteIdent(n)} IS NOT NULL"));
        var ordinal = table.OrdinalName();
        var select = string.Join(", ",
            table.Columns.Select(c => DuckTableSql.QuoteIdent(c.Descriptor.Name)));
        var result = DuckTableSql.Run(Kind, table.WithOrdinal(ordinal),
            $"SELECT {select} FROM t WHERE {keep} ORDER BY {DuckTableSql.QuoteIdent(ordinal)}");
        var dropped = table.Rows.Count - result.Rows.Count;
        if (dropped > 0)
            context.Warn($"{Kind}: dropped {dropped} row(s) with nulls.");
        return [new TableValue(result)];
    }
}
