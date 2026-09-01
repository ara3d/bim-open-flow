using Ara3D.BimOpenSchema.DuckDb;
using Ara3D.DataFlowEngine.Abstractions;

namespace BimOpenFlow.Nodes.TableOps;

/// <summary>Wide to long: folds the chosen columns into name/value rows, keeping
/// the id columns as-is. Mixed-type columns widen to text with a warning; null
/// cells produce no row.</summary>
public sealed class TableUnpivotNode : IFlowNode
{
    public const string Kind = "table.unpivot";

    public NodeSpec Spec { get; } = new(
        Kind, 1, NodeCapability.Pure,
        Inputs: [new PortSpec("table", PortType.Table)],
        Outputs: [new PortSpec("table", PortType.Table)],
        Params:
        [
            new ParamSpec("keep", ParamKind.Text),
            new ParamSpec("columns", ParamKind.Text),
            new ParamSpec("nameColumn", ParamKind.Text, "name"),
            new ParamSpec("valueColumn", ParamKind.Text, "value"),
        ],
        "Unpivots columns into name/value rows, keeping the 'keep' columns as row ids.");

    public IReadOnlyList<FlowValue> Eval(IEvalContext context,
        IReadOnlyList<FlowValue> inputs, ParamValues parameters)
    {
        var table = inputs.TableInput(0, Kind);
        var keep = parameters.GetText("keep").SplitNames()
            .Select(n => table.CanonicalName(n, Kind)).ToList();
        var named = parameters.GetText("columns").SplitNames()
            .Select(n => table.CanonicalName(n, Kind)).ToList();
        var columns = named.Count > 0
            ? named
            : table.Names().Where(n => !keep.Contains(n, StringComparer.OrdinalIgnoreCase)).ToList();
        if (columns.Count == 0)
            throw new ArgumentException($"{Kind}: no columns to unpivot.");
        // Absence is reported, never silent: an explicit columns list drops
        // everything neither kept nor unpivoted — say so.
        if (named.Count > 0)
        {
            var dropped = table.Names()
                .Where(n => !keep.Contains(n, StringComparer.OrdinalIgnoreCase)
                    && !columns.Contains(n, StringComparer.OrdinalIgnoreCase)).ToList();
            if (dropped.Count > 0)
                context.Warn($"{Kind}: columns not kept or unpivoted are dropped: {string.Join(", ", dropped)}.");
        }
        var nameColumn = parameters.GetText("nameColumn", "name");
        var valueColumn = parameters.GetText("valueColumn", "value");
        foreach (var output in new[] { nameColumn, valueColumn })
            if (keep.Contains(output, StringComparer.OrdinalIgnoreCase))
                throw new ArgumentException($"{Kind}: output column '{output}' collides with a kept column.");

        var kinds = columns
            .Select(n => TableColumns.KindName(table.Columns[table.RequireColumn(n, Kind)].Descriptor.Type))
            .Distinct().ToList();
        var widen = kinds.Count > 1;
        if (widen)
            context.Warn($"{Kind}: columns have mixed types; values widened to text.");

        var ord = TableColumns.FreeName("__row__", table);
        var keepCols = keep.Select(DuckTableSql.QuoteIdent).ToList();
        var sourceCols = columns.Select(n =>
            widen ? $"CAST({n.Ident()} AS VARCHAR) AS {n.Ident()}" : n.Ident());
        var inner = string.Join(", ", keepCols.Append(ord.Ident()).Concat(sourceCols));
        var position = $"CASE {nameColumn.Ident()} " + string.Join(" ",
            columns.Select((n, i) => $"WHEN {n.Literal()} THEN {i}")) + " END";
        var outCols = string.Join(", ",
            keepCols.Append(nameColumn.Ident()).Append(valueColumn.Ident()));
        var sql = $"""
            SELECT {outCols} FROM (
              UNPIVOT (SELECT {inner} FROM t)
              ON {string.Join(", ", columns.Select(DuckTableSql.QuoteIdent))}
              INTO NAME {nameColumn.Ident()} VALUE {valueColumn.Ident()})
            ORDER BY {ord.Ident()}, {position}
            """;
        return [new TableValue(DuckTableSql.Run(Kind, table.WithOrdinal(ord), sql))];
    }
}
