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
    {
        var table = inputs.TableInput(0, Kind);
        var sort = parameters.RequiredEnum("sort", Kind, "none", Sorts);
        var label = VizProjection.OptionalColumn(context, table,
            parameters.GetText("labelColumn"), Kind);
        if (label < 0)
            label = VizProjection.FirstTextColumn(table);
        var values = VizProjection.ValueColumns(context, table,
            parameters.GetText("valueColumns"), label, Kind);
        var columns = label >= 0 ? values.Prepend(label).ToList() : values;
        var order = sort != "none" && values.Count > 0
            ? VizProjection.SortedRows(table, values[0], sort == "asc")
            : null;
        return [new TableValue(VizProjection.Project(table, columns, order))];
    }
}
