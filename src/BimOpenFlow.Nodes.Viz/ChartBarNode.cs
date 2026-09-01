using Ara3D.DataFlowEngine.Abstractions;

namespace BimOpenFlow.Nodes.Viz;

/// <summary>Validates and projects table data for a bar chart: the label
/// column first, then the value columns; one bar (group) per row. The chart
/// pane renders output + params.</summary>
public sealed class ChartBarNode : IFlowNode
{
    public const string Kind = "chart.bar";

    private static readonly string[] Sorts = ["none", "asc", "desc"];

    public NodeSpec Spec { get; } = new(
        Kind, 1, NodeCapability.Pure,
        Inputs: [new PortSpec("table", PortType.Table)],
        Outputs: [new PortSpec("table", PortType.Table)],
        Params:
        [
            new ParamSpec("labelColumn", ParamKind.Text, Suggest: SuggestSource.ColumnsOf("table")),
            new ParamSpec("valueColumns", ParamKind.Text, Suggest: SuggestSource.ColumnsOf("table")),
            new ParamSpec("title", ParamKind.Text),
            new ParamSpec("sort", ParamKind.Enum, "none", Sorts),
        ],
        "Projects 'labelColumn' plus the comma-separated numeric 'valueColumns' "
        + "for the bar-chart pane; 'sort' orders rows by the first value column.");

    public IReadOnlyList<FlowValue> Eval(IEvalContext context,
        IReadOnlyList<FlowValue> inputs, ParamValues parameters)
        => throw new NotImplementedException("Track PACK implements");
}
