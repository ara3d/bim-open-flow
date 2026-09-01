using Ara3D.DataFlowEngine.Abstractions;

namespace BimOpenFlow.Nodes.Bos;

/// <summary>Sorts via DuckDB. The 'by' parameter is comma-separated column names,
/// each with an optional ' desc' (or explicit ' asc') suffix.</summary>
// TODO: column names containing commas or spaces cannot be expressed in 'by'; add a
// quoting syntax if such columns show up in practice.
public sealed class TableSortNode : IFlowNode
{
    public const string Kind = "table.sort";

    public NodeSpec Spec { get; } = new(
        Kind, 1, NodeCapability.Pure,
        Inputs: [new PortSpec("table", PortType.Table)],
        Outputs: [new PortSpec("table", PortType.Table)],
        Params: [new ParamSpec("by", ParamKind.Text, Suggest: SuggestSource.ColumnsOf("table"))],
        "Sorts by comma-separated column names, each optionally suffixed with ' desc'.");

    public IReadOnlyList<FlowValue> Eval(IEvalContext context, IReadOnlyList<FlowValue> inputs, ParamValues parameters)
    {
        var table = inputs.TableInput(0, Kind);
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
