using System.Text.RegularExpressions;
using Ara3D.DataFlowEngine.Abstractions;
using Ara3D.DataFlowEngine.Expressions;
using Ara3D.DataTable;
using SchemaRules = BimOpenFlow.Relations.SchemaRules;
using SchemaInputs = BimOpenFlow.Relations.SchemaInputs;
using Aggregation = BimOpenFlow.Relations.Aggregation;
using AggregateFunction = BimOpenFlow.Relations.AggregateFunction;

namespace BimOpenFlow.Nodes.TableOps;

/// <summary>Groups and aggregates via DuckDB. Aggregates are written as
/// "func(column) as name" with funcs count/sum/min/max/avg; count also accepts *.
/// Sums are cast (BIGINT for integer columns, DOUBLE otherwise) so the result column
/// type is predictable instead of DuckDB's HUGEINT. Output rows are ordered by the
/// group columns for determinism.</summary>
public sealed partial class TableAggregateNode : ISchemaNode
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
            new ParamSpec("groupBy", ParamKind.Text, Suggest: SuggestSource.ColumnsOf("table"), List: true),
            new ParamSpec("aggregates", ParamKind.Text, Suggest: SuggestSource.ColumnsOf("table", ColumnUse.Aggregate), List: true),
        ],
        "Groups by the comma-separated groupBy columns (may be empty) and computes "
        + "comma-separated 'func(column) as name' aggregates (count/sum/min/max/avg).",
        Schemas: [SchemaSpec.Inferred("table")]);

    private static readonly Dictionary<string, AggregateFunction> Functions = new()
    {
        ["count"] = AggregateFunction.Count, ["sum"] = AggregateFunction.Sum, ["min"] = AggregateFunction.Min,
        ["max"] = AggregateFunction.Max, ["avg"] = AggregateFunction.Avg,
    };

    /// <summary>Parses one 'func(column) as name'; evaluation and the shape rule share it. The column is
    /// null for count(*).</summary>
    internal static Aggregation ParseAggregation(string spec)
    {
        var match = AggregateSpec().Match(spec);
        if (!match.Success)
            throw new ArgumentException($"{Kind}: aggregate '{spec}' must look like 'func(column) as name'.");

        var func = match.Groups["func"].Value.ToLowerInvariant();
        if (!Functions.TryGetValue(func, out var function))
            throw new ArgumentException($"{Kind}: unknown aggregate function '{func}' in '{spec}'.");

        var col = match.Groups["col"].Value;
        if (col == "*" && function != AggregateFunction.Count)
            throw new ArgumentException($"{Kind}: only count may aggregate over '*' ('{spec}').");
        return new Aggregation(function, col == "*" ? null : col, match.Groups["name"].Value.Trim());
    }

    private static IReadOnlyList<Aggregation> ReadAggregations(ParamValues parameters)
        => parameters.RequiredText("aggregates", Kind).SplitNames().Select(ParseAggregation).ToList();

    public SchemaResult Infer(string output, IReadOnlyList<SchemaInput> inputs, ParamValues parameters, ISchemaCatalog catalog)
        => SchemaInputs.Run(Spec, inputs, schemas => SchemaRules.Aggregate(schemas[0],
            parameters.GetText("groupBy").SplitNames(), ReadAggregations(parameters)), throughDuckDb: true);

    public IReadOnlyList<FlowValue> Eval(IEvalContext context, IReadOnlyList<FlowValue> inputs, ParamValues parameters)
    {
        var table = inputs.TableInput(0, Kind);
        var groups = parameters.GetText("groupBy").SplitNames()
            .Select(g => table.CanonicalName(g, Kind))
            .ToList();
        var aggregates = ReadAggregations(parameters)
            .Select(a => ToSql(a, table))
            .ToList();

        var selects = string.Join(", ", groups.Select(QuoteId).Concat(aggregates));
        var grouping = groups.Count == 0
            ? ""
            : $" GROUP BY {string.Join(", ", groups.Select(QuoteId))} ORDER BY {string.Join(", ", groups.Select(QuoteId))}";
        return [new TableValue(table.QueryOver($"SELECT {selects} FROM t{grouping}", "aggregate"))];
    }

    private static string ToSql(Aggregation aggregation, IDataTable table)
    {
        var alias = QuoteId(aggregation.Name);
        if (aggregation.Column is null)
            return $"count(*) AS {alias}";

        var func = aggregation.Function.ToString().ToLowerInvariant();
        var column = table.Columns[table.RequireColumn(aggregation.Column, Kind)];
        var quoted = QuoteId(column.Descriptor.Name);
        return aggregation.Function == AggregateFunction.Sum
            ? $"CAST(sum({quoted}) AS {SumType(column)}) AS {alias}"
            : $"{func}({quoted}) AS {alias}";
    }

    private static string SumType(IDataColumn column)
        => TableExpressions.ToScalarType(column.Descriptor.Type) == ScalarType.Integer ? "BIGINT" : "DOUBLE";

    private static string QuoteId(string name)
        => name.Ident();
}
