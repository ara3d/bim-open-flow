using Ara3D.BimOpenSchema.DuckDb;
using Ara3D.DataFlowEngine.Abstractions;
using SchemaRules = BimOpenFlow.Relations.SchemaRules;
using SchemaInputs = BimOpenFlow.Relations.SchemaInputs;

namespace BimOpenFlow.Nodes.Cleaning;

/// <summary>Drops rows where any (or all) of the listed columns are null,
/// preserving row order and warning with the dropped-row count.</summary>
public sealed class TableDropNullsNode : ISchemaNode
{
    public const string Kind = "table.dropNulls";

    public NodeSpec Spec { get; } = new(
        Kind, 1, NodeCapability.Pure,
        Inputs: [new PortSpec("table", PortType.Table)],
        Outputs: [new PortSpec("table", PortType.Table)],
        Params:
        [
            new ParamSpec("columns", ParamKind.Text, Suggest: SuggestSource.ColumnsOf("table"), List: true),
            new ParamSpec("mode", ParamKind.Enum, "any", ["any", "all"]),
        ],
        "Drops rows where any/all of 'columns' (empty = all columns) are null; warns with the dropped count.",
        Schemas: [SchemaSpec.Inferred("table")]);

    public SchemaResult Infer(string output, IReadOnlyList<SchemaInput> inputs, ParamValues parameters, ISchemaCatalog catalog)
        => SchemaInputs.Run(Spec, inputs, schemas => SchemaRules.DropNulls(schemas[0],
            parameters.GetText("columns").SplitNames(), ReadMode(parameters) == "any"), throughDuckDb: true);

    private static string ReadMode(ParamValues parameters)
        => parameters.RequiredEnum("mode", Kind, "any", "any", "all");

    public IReadOnlyList<FlowValue> Eval(IEvalContext context,
        IReadOnlyList<FlowValue> inputs, ParamValues parameters)
    {
        var table = inputs.TableInput(0, Kind);
        var names = parameters.GetText("columns").SplitNames()
            .Select(n => table.RequireColumn(n, Kind).Descriptor.Name).ToList();
        if (names.Count == 0)
            names = table.Columns.Select(c => c.Descriptor.Name).ToList();
        var joiner = ReadMode(parameters) == "any" ? " AND " : " OR ";
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
