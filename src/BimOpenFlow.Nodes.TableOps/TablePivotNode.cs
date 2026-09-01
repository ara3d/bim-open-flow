using System.Globalization;
using Ara3D.BimOpenSchema.DuckDb;
using Ara3D.DataFlowEngine.Abstractions;

namespace BimOpenFlow.Nodes.TableOps;

/// <summary>Long to wide: the distinct values of nameColumn become new columns,
/// filled by aggregating valueColumn per group. New columns are ordered by
/// sorted distinct value; rows are ordered by the group-by columns.</summary>
public sealed class TablePivotNode : IFlowNode
{
    public const string Kind = "table.pivot";

    public NodeSpec Spec { get; } = new(
        Kind, 1, NodeCapability.Pure,
        Inputs: [new PortSpec("table", PortType.Table)],
        Outputs: [new PortSpec("table", PortType.Table)],
        Params:
        [
            new ParamSpec("groupBy", ParamKind.Text),
            new ParamSpec("nameColumn", ParamKind.Text),
            new ParamSpec("valueColumn", ParamKind.Text),
            new ParamSpec("aggregate", ParamKind.Enum, "first",
                ["first", "sum", "count", "min", "max", "avg"]),
        ],
        "Pivots long data wide: one new column per distinct value of nameColumn.");

    public IReadOnlyList<FlowValue> Eval(IEvalContext context,
        IReadOnlyList<FlowValue> inputs, ParamValues parameters)
    {
        var table = inputs.TableInput(0, Kind);
        var groupBy = parameters.RequiredText("groupBy", Kind).SplitNames()
            .Select(n => table.CanonicalName(n, Kind)).ToList();
        if (groupBy.Count == 0)
            throw new ArgumentException($"{Kind}: parameter 'groupBy' names no columns.");
        var nameColumn = table.CanonicalName(parameters.RequiredText("nameColumn", Kind), Kind);
        var valueColumn = table.CanonicalName(parameters.RequiredText("valueColumn", Kind), Kind);
        var aggregate = parameters.RequiredEnum("aggregate", Kind, "first",
            "first", "sum", "count", "min", "max", "avg");

        var groupCols = string.Join(", ", groupBy.Select(DuckTableSql.QuoteIdent));
        var distinct = DuckTableSql.Run(Kind, table,
            $"SELECT DISTINCT {nameColumn.Ident()} FROM t WHERE {nameColumn.Ident()} IS NOT NULL ORDER BY 1");
        var values = Enumerable.Range(0, distinct.RowCount())
            .Select(row => distinct[0, row]).ToList();
        if (values.Count == 0)
            return [new TableValue(DuckTableSql.Run(Kind, table,
                $"SELECT DISTINCT {groupCols} FROM t ORDER BY {groupCols}"))];

        var ord = TableColumns.FreeName("__row__", table);
        var valueKind = TableColumns.KindName(
            table.Columns[table.RequireColumn(valueColumn, Kind)].Descriptor.Type);
        var agg = aggregate switch
        {
            "first" => $"arg_min({valueColumn.Ident()}, {ord.Ident()})",
            // sum over BIGINT yields HUGEINT, which the wire cannot carry
            "sum" when valueKind == "Integer" => $"CAST(sum({valueColumn.Ident()}) AS BIGINT)",
            _ => $"{aggregate}({valueColumn.Ident()})",
        };
        var literals = string.Join(", ", values.Select(ValueLiteral));
        var valueNames = string.Join(", ",
            values.Select(v => TableColumns.CellText(v)!.Ident()));
        var sql = $"""
            SELECT {groupCols}, {valueNames} FROM (
              PIVOT t ON {nameColumn.Ident()} IN ({literals}) USING {agg} GROUP BY {groupCols})
            ORDER BY {groupCols}
            """;
        return [new TableValue(DuckTableSql.Run(Kind, table.WithOrdinal(ord), sql))];
    }

    private static string ValueLiteral(object? value)
        => value switch
        {
            string s => s.Literal(),
            bool b => b ? "true" : "false",
            IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
            _ => TableColumns.CellText(value)!.Literal(),
        };
}
