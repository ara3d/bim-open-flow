using Ara3D.BimOpenSchema.DuckDb;
using Ara3D.DataFlowEngine.Abstractions;
using SchemaRules = BimOpenFlow.Relations.SchemaRules;
using SchemaInputs = BimOpenFlow.Relations.SchemaInputs;

namespace BimOpenFlow.Nodes.TableOps;

/// <summary>Whole-row distinct, or first-row-per-key when key columns are named.
/// Row order follows the first occurrence in the input.</summary>
public sealed class TableDistinctNode : ISchemaNode
{
    public const string Kind = "table.distinct";

    public NodeSpec Spec { get; } = new(
        Kind, 1, NodeCapability.Pure,
        Inputs: [new PortSpec("table", PortType.Table)],
        Outputs: [new PortSpec("table", PortType.Table)],
        Params: [new ParamSpec("columns", ParamKind.Text, Suggest: SuggestSource.ColumnsOf("table"), List: true)],
        "Removes duplicate rows; with key columns named, keeps the first row per key with all columns.",
        Schemas: [SchemaSpec.Inferred("table")]);

    public SchemaResult Infer(string output, IReadOnlyList<SchemaInput> inputs, ParamValues parameters, ISchemaCatalog catalog)
        => SchemaInputs.Run(Spec, inputs, schemas => SchemaRules.Distinct(schemas[0], parameters.GetText("columns").SplitNames()));

    public IReadOnlyList<FlowValue> Eval(IEvalContext context,
        IReadOnlyList<FlowValue> inputs, ParamValues parameters)
    {
        var table = inputs.TableInput(0, Kind);
        var keys = parameters.GetText("columns").SplitNames()
            .Select(n => table.CanonicalName(n, Kind)).ToList();
        var partition = (keys.Count > 0 ? keys : table.Names()).Select(DuckTableSql.QuoteIdent);
        var ord = TableColumns.FreeName("__row__", table);
        var rn = TableColumns.FreeName("__rn__", table);
        var cols = string.Join(", ", table.Names().Select(DuckTableSql.QuoteIdent));
        var sql = $"""
            SELECT {cols} FROM (
              SELECT *, row_number() OVER (PARTITION BY {string.Join(", ", partition)} ORDER BY {ord.Ident()}) AS {rn.Ident()}
              FROM t)
            WHERE {rn.Ident()} = 1 ORDER BY {ord.Ident()}
            """;
        return [new TableValue(DuckTableSql.Run(Kind, table.WithOrdinal(ord), sql))];
    }
}
