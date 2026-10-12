using Ara3D.BimOpenSchema.DuckDb;
using Ara3D.DataFlowEngine.Abstractions;
using SchemaRules = BimOpenFlow.Relations.SchemaRules;
using SchemaInputs = BimOpenFlow.Relations.SchemaInputs;

namespace BimOpenFlow.Nodes.TableOps;

/// <summary>Splits one text column on a separator into new columns, one per
/// requested name. Fewer parts than names yields nulls; extra parts are dropped.</summary>
public sealed class TableSplitColumnNode : ISchemaNode
{
    public const string Kind = "table.splitColumn";

    public NodeSpec Spec { get; } = new(
        Kind, 1, NodeCapability.Pure,
        Inputs: [new PortSpec("table", PortType.Table)],
        Outputs: [new PortSpec("table", PortType.Table)],
        Params:
        [
            new ParamSpec("column", ParamKind.Text, Suggest: SuggestSource.ColumnsOf("table")),
            new ParamSpec("separator", ParamKind.Text, "-"),
            new ParamSpec("names", ParamKind.Text, List: true),
            new ParamSpec("keep", ParamKind.Boolean, "false"),
        ],
        "Splits a column on a separator into new columns named by 'names'.",
        Schemas: [SchemaSpec.Inferred("table")]);

    /// <summary>The parameters, checked; evaluation and the shape rule both read them here.</summary>
    private readonly record struct Args(string Column, string Separator, IReadOnlyList<string> Names, bool Keep)
    {
        public static Args Read(ParamValues parameters)
        {
            var separator = parameters.GetText("separator", "-");
            return separator.Length == 0
                ? throw new ArgumentException($"{Kind}: parameter 'separator' must not be empty.")
                : new Args(parameters.RequiredText("column", Kind), separator,
                    parameters.RequiredText("names", Kind).SplitNames(), parameters.GetBoolean("keep"));
        }
    }

    public SchemaResult Infer(string output, IReadOnlyList<SchemaInput> inputs, ParamValues parameters, ISchemaCatalog catalog)
        => SchemaInputs.Run(Spec, inputs, schemas =>
        {
            var args = Args.Read(parameters);
            return SchemaRules.SplitColumn(schemas[0], args.Column, args.Names, args.Keep);
        }, throughDuckDb: true);

    public IReadOnlyList<FlowValue> Eval(IEvalContext context,
        IReadOnlyList<FlowValue> inputs, ParamValues parameters)
    {
        var table = inputs.TableInput(0, Kind);
        var (columnParameter, separator, names, keep) = Args.Read(parameters);
        var column = table.CanonicalName(columnParameter, Kind);

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var name in names)
        {
            if (table.ColumnIndex(name) >= 0)
                throw new ArgumentException($"{Kind}: new column '{name}' already exists.");
            if (!seen.Add(name))
                throw new ArgumentException($"{Kind}: new column '{name}' is named more than once.");
        }

        var sep = separator.Literal();
        var parts = names.Select((name, i) =>
            $"CASE WHEN len(string_split({column.Ident()}, {sep})) >= {i + 1} " +
            $"THEN split_part({column.Ident()}, {sep}, {i + 1}) END AS {name.Ident()}");
        var source = keep ? "*" : $"* EXCLUDE ({column.Ident()})";
        var sql = $"SELECT {source}, {string.Join(", ", parts)} FROM t";
        return [new TableValue(DuckTableSql.Run(Kind, table, sql))];
    }
}
