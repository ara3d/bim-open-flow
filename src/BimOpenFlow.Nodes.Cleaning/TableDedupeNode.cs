using Ara3D.BimOpenSchema.DuckDb;
using Ara3D.DataFlowEngine.Abstractions;

namespace BimOpenFlow.Nodes.Cleaning;

/// <summary>Keeps one row per key combination — the first or last by 'orderBy'
/// (table.sort syntax; empty = input row order) — preserving the input order of
/// the kept rows and warning with the duplicate count.</summary>
public sealed class TableDedupeNode : IFlowNode
{
    public const string Kind = "table.dedupe";

    public NodeSpec Spec { get; } = new(
        Kind, 1, NodeCapability.Pure,
        Inputs: [new PortSpec("table", PortType.Table)],
        Outputs: [new PortSpec("table", PortType.Table)],
        Params:
        [
            new ParamSpec("keys", ParamKind.Text, Suggest: SuggestSource.ColumnsOf("table")),
            new ParamSpec("keep", ParamKind.Enum, "first", ["first", "last"]),
            new ParamSpec("orderBy", ParamKind.Text, Suggest: SuggestSource.ColumnsOf("table")),
        ],
        "Keeps the first/last row per 'keys' by 'orderBy' (empty = input row order); warns with the duplicate count.");

    public IReadOnlyList<FlowValue> Eval(IEvalContext context,
        IReadOnlyList<FlowValue> inputs, ParamValues parameters)
    {
        var table = inputs.TableInput(0, Kind);
        var keys = parameters.RequiredText("keys", Kind).SplitNames()
            .Select(n => DuckTableSql.QuoteIdent(table.RequireColumn(n, Kind).Descriptor.Name)).ToList();
        var keep = parameters.RequiredEnum("keep", Kind, "first", "first", "last");
        var reverse = keep == "last";
        var ordinal = table.OrdinalName();
        var terms = parameters.GetText("orderBy").SplitNames().Select(Term).ToList();
        terms.Add($"{DuckTableSql.QuoteIdent(ordinal)} {(reverse ? "DESC" : "ASC")}");
        var select = string.Join(", ",
            table.Columns.Select(c => DuckTableSql.QuoteIdent(c.Descriptor.Name)));
        var result = DuckTableSql.Run(Kind, table.WithOrdinal(ordinal),
            $"SELECT {select} FROM t QUALIFY row_number() OVER (PARTITION BY {string.Join(", ", keys)} " +
            $"ORDER BY {string.Join(", ", terms)}) = 1 ORDER BY {DuckTableSql.QuoteIdent(ordinal)}");
        var duplicates = table.Rows.Count - result.Rows.Count;
        if (duplicates > 0)
            context.Warn($"{Kind}: removed {duplicates} duplicate row(s).");
        return [new TableValue(result)];

        string Term(string entry)
        {
            var tokens = entry.Split(' ', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            var ascending = tokens.Length switch
            {
                1 => true,
                2 when tokens[1].Equals("asc", StringComparison.OrdinalIgnoreCase) => true,
                2 when tokens[1].Equals("desc", StringComparison.OrdinalIgnoreCase) => false,
                _ => throw new ArgumentException($"{Kind}: cannot parse orderBy term '{entry}'."),
            };
            var direction = ascending != reverse ? "ASC" : "DESC";
            return $"{DuckTableSql.QuoteIdent(table.RequireColumn(tokens[0], Kind).Descriptor.Name)} {direction}";
        }
    }
}
