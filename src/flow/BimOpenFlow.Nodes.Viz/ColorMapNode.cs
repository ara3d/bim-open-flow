using Ara3D.DataFlowEngine.Abstractions;
using BimOpenFlow.Nodes.Support;

namespace BimOpenFlow.Nodes.Viz;

/// <summary>Exposes ColorScale.Build as a node: one column of a values table
/// becomes a legend table, so that a chart pane and a 3D pane wired to the
/// same node's output share one colour scale.</summary>
public sealed class ColorMapNode : IFlowNode
{
    public const string Kind = "view.colormap";

    public NodeSpec Spec { get; } = new(
        Kind, 1, NodeCapability.Pure,
        Inputs: [new PortSpec("values", PortType.Table)],
        Outputs: [new PortSpec("legend", PortType.Table)],
        Params:
        [
            new ParamSpec("valueColumn", ParamKind.Text, Suggest: SuggestSource.ColumnsOf("values")),
            new ParamSpec("colorMap", ParamKind.Enum, "viridis", ColorScale.ColorMapNames),
            new ParamSpec("auto", ParamKind.Boolean, "true"),
            new ParamSpec("min", ParamKind.Number, "0"),
            new ParamSpec("max", ParamKind.Number, "1"),
        ],
        "Builds a legend table (the shared colour scale) for 'valueColumn' of "
        + "'values'; wire the 'legend' output into view3d.color's or chart.bar's "
        + "'scale' input so both panes share one colour scale. An unknown column "
        + "warns and emits an empty legend.");

    public IReadOnlyList<FlowValue> Eval(IEvalContext context,
        IReadOnlyList<FlowValue> inputs, ParamValues parameters)
    {
        var values = inputs.TableInput(0, Kind);
        var colorMap = parameters.RequiredEnum("colorMap", Kind, "viridis", ColorScale.ColorMapNames);
        var column = VizProjection.OptionalColumn(context, values, parameters.GetText("valueColumn"), Kind);
        if (column < 0)
            return [new TableValue(ColorScale.EmptyTable())];

        var scale = ColorScale.Build(values, column, colorMap,
            parameters.GetBoolean("auto", true), parameters.GetNumber("min", 0), parameters.GetNumber("max", 1),
            context, Kind);
        return [new TableValue(scale.ToTable())];
    }
}
