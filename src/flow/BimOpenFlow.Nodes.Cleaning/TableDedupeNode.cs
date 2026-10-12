using Ara3D.BimOpenSchema.DuckDb;
using Ara3D.DataFlowEngine.Abstractions;
using SchemaRules = BimOpenFlow.Relations.SchemaRules;
using SchemaInputs = BimOpenFlow.Relations.SchemaInputs;

namespace BimOpenFlow.Nodes.Cleaning;

/// <summary>Keeps one row per key combination — the first or last by 'orderBy'
/// (table.sort syntax; empty = input row order) — preserving the input order of
/// the kept rows and warning with the duplicate count.</summary>
public sealed class TableDedupeNode : ISchemaNode
{
    public const string Kind = "table.dedupe";

    public NodeSpec Spec { get; } = new(
        Kind, 1, NodeCapability.Pure,
        Inputs: [new PortSpec("table", PortType.Table)],
        Outputs: [new PortSpec("table", PortType.Table)],
        Params:
        [
            new ParamSpec("keys", ParamKind.Text, Suggest: SuggestSource.ColumnsOf("table"), List: true),
            new ParamSpec("keep", ParamKind.Enum, "first", ["first", "last"]),
            new ParamSpec("orderBy", ParamKind.Text, Suggest: SuggestSource.ColumnsOf("table"), List: true),
        ],
        "Keeps the first/last row per 'keys' by 'orderBy' (empty = input row order); warns with the duplicate count.",
        Schemas: [SchemaSpec.Inferred("table")]);

    /// <summary>The parameters, checked; evaluation and the shape rule both read them here.
    /// OrderBy holds (column, ascending) pairs from terms like "rev desc".</summary>
    private readonly record struct Args(IReadOnlyList<string> Keys, bool Last, IReadOnlyList<(string Column, bool Ascending)> OrderBy)
    {
        public static Args Read(ParamValues parameters)
            => new(parameters.RequiredText("keys", Kind).SplitNames(),
                parameters.RequiredEnum("keep", Kind, "first", "first", "last") == "last",
                parameters.GetText("orderBy").SplitNames().Select(ParseTerm).ToList());

        private static (string, bool) ParseTerm(string entry)
        {
            var tokens = entry.Split(' ', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            return tokens.Length switch
            {
                1 => (tokens[0], true),
                2 when tokens[1].Equals("asc", StringComparison.OrdinalIgnoreCase) => (tokens[0], true),
                2 when tokens[1].Equals("desc", StringComparison.OrdinalIgnoreCase) => (tokens[0], false),
                _ => throw new ArgumentException($"{Kind}: cannot parse orderBy term '{entry}'."),
            };
        }
    }

    /// <summary>Dedupe keeps every column; the key and order columns must exist.</summary>
    public SchemaResult Infer(string output, IReadOnlyList<SchemaInput> inputs, ParamValues parameters, ISchemaCatalog catalog)
        => SchemaInputs.Run(Spec, inputs, schemas =>
        {
            var args = Args.Read(parameters);
            return SchemaRules.Distinct(schemas[0], args.Keys.Concat(args.OrderBy.Select(o => o.Column)));
        }, throughDuckDb: true);

    public IReadOnlyList<FlowValue> Eval(IEvalContext context,
        IReadOnlyList<FlowValue> inputs, ParamValues parameters)
    {
        var table = inputs.TableInput(0, Kind);
        var args = Args.Read(parameters);
        var keys = args.Keys.Select(n => DuckTableSql.QuoteIdent(table.RequireColumn(n, Kind).Descriptor.Name)).ToList();
        var reverse = args.Last;
        var ordinal = table.OrdinalName();
        var terms = args.OrderBy.Select(Term).ToList();
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

        string Term((string Column, bool Ascending) term)
            => $"{DuckTableSql.QuoteIdent(table.RequireColumn(term.Column, Kind).Descriptor.Name)} {(term.Ascending != reverse ? "ASC" : "DESC")}";
    }
}
