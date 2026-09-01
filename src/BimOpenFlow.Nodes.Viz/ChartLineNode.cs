using Ara3D.DataFlowEngine.Abstractions;

namespace BimOpenFlow.Nodes.Viz;

/// <summary>Validates and projects table data for a line chart: the x column
/// first, then the y columns; rows ordered by the x column (numeric when the
/// column is numeric, else lexical). The chart pane renders output + params.</summary>
public sealed class ChartLineNode : IFlowNode
{
    public const string Kind = "chart.line";

    public NodeSpec Spec { get; } = new(
        Kind, 1, NodeCapability.Pure,
        Inputs: [new PortSpec("table", PortType.Table)],
        Outputs: [new PortSpec("table", PortType.Table)],
        Params:
        [
            new ParamSpec("xColumn", ParamKind.Text, Suggest: SuggestSource.ColumnsOf("table")),
            new ParamSpec("yColumns", ParamKind.Text, Suggest: SuggestSource.ColumnsOf("table")),
            new ParamSpec("title", ParamKind.Text),
        ],
        "Projects 'xColumn' plus the comma-separated numeric 'yColumns' for the "
        + "line-chart pane; rows are ordered by 'xColumn'.");

    public IReadOnlyList<FlowValue> Eval(IEvalContext context,
        IReadOnlyList<FlowValue> inputs, ParamValues parameters)
    {
        var table = inputs.TableInput(0, Kind);
        var x = VizProjection.OptionalColumn(context, table,
            parameters.GetText("xColumn"), Kind);
        var ys = VizProjection.ValueColumns(context, table,
            parameters.GetText("yColumns"), x, Kind);
        var columns = x >= 0 ? ys.Prepend(x).ToList() : ys;
        var order = x >= 0 ? VizProjection.SortedRows(table, x, true) : null;
        return [new TableValue(VizProjection.Project(table, columns, order))];
    }
}
