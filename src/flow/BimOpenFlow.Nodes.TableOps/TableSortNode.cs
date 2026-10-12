using Ara3D.DataFlowEngine.Abstractions;
using BimOpenFlow.Relations;

namespace BimOpenFlow.Nodes.TableOps;

/// <summary>Sorts by three exact column names with independent directions.
/// Legacy graphs may still use the comma-separated 'by' parameter.</summary>
public sealed class TableSortNode : ISchemaNode
{
    public const string Kind = "table.sort";

    public NodeSpec Spec { get; } = new(
        Kind, 1, NodeCapability.Pure,
        Inputs: [new PortSpec("table", PortType.Table)],
        Outputs: [new PortSpec("table", PortType.Table)],
        Params: [
            new ParamSpec("A", ParamKind.Text, Suggest: SuggestSource.ColumnsOf("table"), Control: new ParamControl("sortColumn")),
            new ParamSpec("B", ParamKind.Text, Suggest: SuggestSource.ColumnsOf("table"), Control: new ParamControl("sortColumn")),
            new ParamSpec("C", ParamKind.Text, Suggest: SuggestSource.ColumnsOf("table"), Control: new ParamControl("sortColumn")),
            new ParamSpec("descendingA", ParamKind.Boolean, "false", Control: new ParamControl("hidden")),
            new ParamSpec("descendingB", ParamKind.Boolean, "false", Control: new ParamControl("hidden")),
            new ParamSpec("descendingC", ParamKind.Boolean, "false", Control: new ParamControl("hidden")),
            new ParamSpec("by", ParamKind.Text, Control: new ParamControl("hidden"), List: true)],
        "Sorts by columns A, B, then C, each with its own ascending or descending direction.",
        Schemas: [SchemaSpec.Inferred("table")]);

    /// <summary>The sort keys: A, B, C when any is set, else the legacy 'by' terms; none means no sort.</summary>
    private static IReadOnlyList<SortKey> ReadKeys(ParamValues parameters)
    {
        var columns = new[] { "A", "B", "C" }.Where(key => parameters.GetText(key).Length > 0).ToArray();
        if (columns.Length > 0)
            return columns.Select(key => new SortKey(parameters.GetText(key), parameters.GetBoolean("descending" + key))).ToList();
        if (parameters.GetText("by").Length == 0)
            return [];
        var keys = SortSyntax.ParseKeys(parameters.RequiredText("by", Kind), Kind);
        return keys.Count > 0 ? keys : throw new ArgumentException($"{Kind}: parameter 'by' names no columns.");
    }

    public SchemaResult Infer(string output, IReadOnlyList<SchemaInput> inputs, ParamValues parameters, ISchemaCatalog catalog)
        => SchemaInputs.Run(Spec, inputs, schemas => SchemaRules.Sort(schemas[0], ReadKeys(parameters).Select(k => k.Column)),
            throughDuckDb: ReadKeys(parameters).Count > 0);

    public IReadOnlyList<FlowValue> Eval(IEvalContext context, IReadOnlyList<FlowValue> inputs, ParamValues parameters)
    {
        var table = inputs.TableInput(0, Kind);
        var keys = ReadKeys(parameters);
        if (keys.Count == 0) return [new TableValue(table)];
        var order = string.Join(", ", keys.Select(key => table.OrderTerm(key, Kind)));
        return [new TableValue(table.QueryOver($"SELECT * FROM t ORDER BY {order}", table.Name))];
    }
}
