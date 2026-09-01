using System.Text.RegularExpressions;
using Ara3D.DataFlowEngine.Abstractions;
using Ara3D.DataFlowEngine.Expressions;
using Ara3D.DataTable;

namespace BimOpenFlow.Nodes.Bos;

/// <summary>Groups and aggregates via DuckDB. Aggregates are written as
/// "func(column) as name" with funcs count/sum/min/max/avg; count also accepts *.
/// Sums are cast (BIGINT for integer columns, DOUBLE otherwise) so the result column
/// type is predictable instead of DuckDB's HUGEINT. Output rows are ordered by the
/// group columns for determinism.</summary>
public sealed partial class TableAggregateNode : IFlowNode
{
    public const string Kind = "table.aggregate";

    [GeneratedRegex(@"^(?<func>[a-zA-Z]+)\s*\(\s*(?<col>\*|[^)]*?)\s*\)\s+as\s+(?<name>.+)$",
        RegexOptions.IgnoreCase)]
    private static partial Regex AggregateSpec();

    public NodeSpec Spec { get; } = new(
        Kind, 1, NodeCapability.Pure,
        Inputs: [new PortSpec("table", PortType.Table)],
        Outputs: [new PortSpec("table", PortType.Table)],
        Params:
        [
            new ParamSpec("groupBy", ParamKind.Text, Suggest: SuggestSource.ColumnsOf("table")),
            new ParamSpec("aggregates", ParamKind.Text),
        ],
        "Groups by the comma-separated groupBy columns (may be empty) and computes "
        + "comma-separated 'func(column) as name' aggregates (count/sum/min/max/avg).");

    public IReadOnlyList<FlowValue> Eval(IEvalContext context, IReadOnlyList<FlowValue> inputs, ParamValues parameters)
    {
        var table = inputs.TableInput(0, Kind);
        var groups = parameters.GetText("groupBy").SplitNames()
            .Select(g => table.RequireColumn(g, Kind).Descriptor.Name)
            .ToList();
        var aggregates = parameters.RequiredText("aggregates", Kind).SplitNames()
            .Select(a => ToSql(a, table))
            .ToList();

        var selects = string.Join(", ", groups.Select(QuoteId).Concat(aggregates));
        var grouping = groups.Count == 0
            ? ""
            : $" GROUP BY {string.Join(", ", groups.Select(QuoteId))} ORDER BY {string.Join(", ", groups.Select(QuoteId))}";
        return [new TableValue(table.QueryOver($"SELECT {selects} FROM t{grouping}", "aggregate"))];
    }

    private static string ToSql(string spec, IDataTable table)
    {
        var match = AggregateSpec().Match(spec);
        if (!match.Success)
            throw new ArgumentException($"{Kind}: aggregate '{spec}' must look like 'func(column) as name'.");

        var func = match.Groups["func"].Value.ToLowerInvariant();
        if (func is not ("count" or "sum" or "min" or "max" or "avg"))
            throw new ArgumentException($"{Kind}: unknown aggregate function '{func}' in '{spec}'.");

        var alias = QuoteId(match.Groups["name"].Value.Trim());
        var col = match.Groups["col"].Value;
        if (col == "*")
            return func == "count"
                ? $"count(*) AS {alias}"
                : throw new ArgumentException($"{Kind}: only count may aggregate over '*' ('{spec}').");

        var column = table.RequireColumn(col, Kind);
        var quoted = QuoteId(column.Descriptor.Name);
        return func == "sum"
            ? $"CAST(sum({quoted}) AS {SumType(column)}) AS {alias}"
            : $"{func}({quoted}) AS {alias}";
    }

    private static string SumType(IDataColumn column)
        => TableExpressions.ToScalarType(column.Descriptor.Type) == ScalarType.Integer ? "BIGINT" : "DOUBLE";

    private static string QuoteId(string name)
        => name.QuoteIdentifier();
}
