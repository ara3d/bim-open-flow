using Ara3D.DataFlowEngine.Abstractions;

namespace BimOpenFlow.Nodes.Bos;

/// <summary>Sorts by three exact column names with independent directions.
/// Legacy graphs may still use the comma-separated 'by' parameter.</summary>
public sealed class TableSortNode : IFlowNode
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
            new ParamSpec("by", ParamKind.Text, Control: new ParamControl("hidden"))],
        "Sorts by columns A, B, then C, each with its own ascending or descending direction.");

    public IReadOnlyList<FlowValue> Eval(IEvalContext context, IReadOnlyList<FlowValue> inputs, ParamValues parameters)
    {
        var table = inputs.TableInput(0, Kind);
        var columns = new[] { "A", "B", "C" }.Where(key => parameters.GetText(key).Length > 0).ToArray();
        if (columns.Length > 0)
        {
            var order = columns.Select(key => table.RequireColumn(parameters.GetText(key), Kind).Descriptor.Name.QuoteIdentifier()
                + (parameters.GetBoolean("descending" + key) ? " DESC" : " ASC"));
            return [new TableValue(table.QueryOver($"SELECT * FROM t ORDER BY {string.Join(", ", order)}", table.Name))];
        }
        if (parameters.GetText("by").Length == 0) return [new TableValue(table)];
        var terms = parameters.RequiredText("by", Kind).SplitNames().Select(Term).ToList();
        if (terms.Count == 0)
            throw new ArgumentException($"{Kind}: parameter 'by' names no columns.");
        return [new TableValue(table.QueryOver(
            $"SELECT * FROM t ORDER BY {string.Join(", ", terms)}", table.Name))];

        string Term(string entry)
        {
            var tokens = entry.Split(' ', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            var direction = tokens.Length switch
            {
                1 => "ASC",
                2 when tokens[1].Equals("desc", StringComparison.OrdinalIgnoreCase) => "DESC",
                2 when tokens[1].Equals("asc", StringComparison.OrdinalIgnoreCase) => "ASC",
                _ => throw new ArgumentException($"{Kind}: cannot parse sort term '{entry}'."),
            };
            return $"{table.RequireColumn(tokens[0], Kind).Descriptor.Name.QuoteIdentifier()} {direction}";
        }
    }
}
