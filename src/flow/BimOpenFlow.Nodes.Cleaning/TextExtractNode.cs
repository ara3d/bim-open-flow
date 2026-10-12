using Ara3D.BimOpenSchema.DuckDb;
using Ara3D.DataFlowEngine.Abstractions;
using SchemaRules = BimOpenFlow.Relations.SchemaRules;
using SchemaInputs = BimOpenFlow.Relations.SchemaInputs;

namespace BimOpenFlow.Nodes.Cleaning;

/// <summary>Extracts one regex capture group from a text column into a new
/// column; rows that do not match get null.</summary>
public sealed class TextExtractNode : ISchemaNode
{
    public const string Kind = "text.extract";

    public NodeSpec Spec { get; } = new(
        Kind, 1, NodeCapability.Pure,
        Inputs: [new PortSpec("table", PortType.Table)],
        Outputs: [new PortSpec("table", PortType.Table)],
        Params:
        [
            new ParamSpec("column", ParamKind.Text, Suggest: SuggestSource.ColumnsOf("table")),
            new ParamSpec("pattern", ParamKind.Text),
            new ParamSpec("group", ParamKind.Integer, "1"),
            new ParamSpec("name", ParamKind.Text),
        ],
        "Adds column 'name' holding capture group 'group' (0 = whole match) of 'pattern' from 'column'; null when no match.",
        Schemas: [SchemaSpec.Inferred("table")]);

    /// <summary>The parameters, checked; evaluation and the shape rule both read them here.</summary>
    private readonly record struct Args(string Column, string Pattern, long Group, string Name)
    {
        public static Args Read(ParamValues parameters)
        {
            var group = parameters.GetInteger("group", 1);
            return group < 0
                ? throw new ArgumentException($"{Kind}: parameter 'group' must be a non-negative integer.")
                : new Args(parameters.RequiredText("column", Kind), parameters.RequiredText("pattern", Kind), group,
                    parameters.RequiredText("name", Kind));
        }
    }

    public SchemaResult Infer(string output, IReadOnlyList<SchemaInput> inputs, ParamValues parameters, ISchemaCatalog catalog)
        => SchemaInputs.Run(Spec, inputs, schemas =>
        {
            var args = Args.Read(parameters);
            return SchemaRules.AppendTextFrom(schemas[0], args.Column, args.Name);
        }, throughDuckDb: true);

    public IReadOnlyList<FlowValue> Eval(IEvalContext context,
        IReadOnlyList<FlowValue> inputs, ParamValues parameters)
    {
        var table = inputs.TableInput(0, Kind);
        var (columnParameter, pattern, group, name) = Args.Read(parameters);
        var column = table.RequireTextColumn(columnParameter, Kind);
        if (table.Columns.Any(c => string.Equals(c.Descriptor.Name, name, StringComparison.OrdinalIgnoreCase)))
            throw new ArgumentException($"{Kind}: column '{name}' already exists.");
        var c = DuckTableSql.QuoteIdent(column.Descriptor.Name);
        var p = DuckTableSql.QuoteLiteral(pattern);
        var extracted =
            $"CASE WHEN regexp_matches({c}, {p}) THEN regexp_extract({c}, {p}, {group}) ELSE NULL END";
        var ordinal = table.OrdinalName();
        var select = string.Join(", ",
            table.Columns.Select(col => DuckTableSql.QuoteIdent(col.Descriptor.Name)));
        return [new TableValue(DuckTableSql.Run(Kind, table.WithOrdinal(ordinal),
            $"SELECT {select}, {extracted} AS {DuckTableSql.QuoteIdent(name)} FROM t " +
            $"ORDER BY {DuckTableSql.QuoteIdent(ordinal)}"))];
    }
}
