using Ara3D.BimOpenSchema.DuckDb;
using Ara3D.DataFlowEngine.Abstractions;

namespace BimOpenFlow.Nodes.DuckDb;

/// <summary>Reads one named table from a .duckdb database file, opened
/// read-only so the node can never mutate the file. The no-SQL companion to
/// duck.query: point at a database, name a table, get the table.</summary>
public sealed class DuckTableNode : ISchemaNode
{
    public const string Kind = "duck.table";

    public NodeSpec Spec { get; } = new(
        Kind, 1, NodeCapability.Pure,
        Inputs: [],
        Outputs: [new PortSpec("table", PortType.Table)],
        Params:
        [
            DuckSourceInput.Spec,
            new ParamSpec("table", ParamKind.Text, Suggest: SuggestSource.TablesInFile("path")),
        ],
        "Reads one named table from a .duckdb database file, read-only.",
        Schemas: [SchemaSpec.Inferred("table")]);

    public SchemaResult Infer(string output, IReadOnlyList<SchemaInput> inputs, ParamValues parameters, ISchemaCatalog catalog)
        => SchemaGuards.Guard(() =>
        {
            var path = DuckSourceInput.PathForSchema(parameters);
            var table = parameters.GetText("table");
            return SchemaGuards.PathProblem(Kind, path)
                   ?? (string.IsNullOrWhiteSpace(table)
                       ? SchemaResult.Fail($"{Kind}: no table is named, so the columns are not known yet.")
                       : DuckSchemas.OnTheWire(catalog.FileSchema("duckdb", path, new Dictionary<string, string> { ["table"] = table })));
        });

    public IReadOnlyList<FlowValue> Eval(IEvalContext context, IReadOnlyList<FlowValue> inputs, ParamValues parameters)
    {
        var path = DuckSourceInput.ResolvePath(parameters, Kind);
        var tableName = parameters.RequiredText("table", Kind);
        using var conn = DuckDbOps.OpenReadOnly(path);
        var known = conn.Query(
            "SELECT 1 FROM information_schema.tables WHERE table_schema = 'main' "
            + $"AND table_name = {DuckTableSql.QuoteLiteral(tableName)}");
        if (known.Rows.Count == 0)
            throw new ArgumentException($"{Kind}: table '{tableName}' not found in {path}.");
        return [new TableValue(
            conn.Query($"SELECT * FROM {DuckTableSql.QuoteIdent(tableName)}", tableName).NormalizeDatesToText())];
    }
}
