using Ara3D.DataFlowEngine.Abstractions;
using Ara3D.DataTable;
using BimOpenFlow.Nodes.Support;

namespace BimOpenFlow.Nodes.Viz;

/// <summary>Validates and projects table data for a bar chart: the label
/// column first, then the value columns; one bar (group) per row. The chart
/// pane renders output + params. An optional `scale` input colours the bars
/// (appended `r g b` columns) and is passed through unchanged on `legend`;
/// without one, `legend` is the empty legend table.</summary>
public sealed class ChartBarNode : IFlowNode
{
    public const string Kind = "chart.bar";

    private static readonly string[] Sorts = ["none", "asc", "desc"];

    public NodeSpec Spec { get; } = new(
        Kind, 1, NodeCapability.Pure,
        Inputs:
        [
            new PortSpec("table", PortType.Table),
            new PortSpec("scale", PortType.Table, Optional: true),
        ],
        Outputs:
        [
            new PortSpec("table", PortType.Table),
            new PortSpec("legend", PortType.Table),
        ],
        Params:
        [
            new ParamSpec("labelColumn", ParamKind.Text, Suggest: SuggestSource.ColumnsOf("table")),
            new ParamSpec("valueColumns", ParamKind.Text, Suggest: SuggestSource.ColumnsOf("table")),
            new ParamSpec("title", ParamKind.Text),
            new ParamSpec("sort", ParamKind.Enum, "none", Sorts),
        ],
        "Projects 'labelColumn' plus the comma-separated numeric 'valueColumns' "
        + "for the bar-chart pane; 'sort' orders rows by the first value column. "
        + "With a 'scale' input, appends 'r g b' colour columns computed from the "
        + "scale's column (or the first value column) and passes the scale through "
        + "on 'legend'; without one, 'legend' is the empty legend table.");

    public IReadOnlyList<FlowValue> Eval(IEvalContext context,
        IReadOnlyList<FlowValue> inputs, ParamValues parameters)
    {
        var table = inputs.TableInput(0, Kind);
        var sort = parameters.RequiredEnum("sort", Kind, "none", Sorts);
        var label = VizProjection.OptionalColumn(context, table,
            parameters.GetText("labelColumn"), Kind);
        if (label < 0)
            label = VizProjection.FirstTextColumn(table);
        // TODO: a valueColumns entry naming the label column duplicates it in
        // the output (and the web pane filters it out) — exclude it here.
        var values = VizProjection.ValueColumns(context, table,
            parameters.GetText("valueColumns"), label, Kind);
        var columns = label >= 0 ? values.Prepend(label).ToList() : values;
        var order = sort != "none" && values.Count > 0
            ? VizProjection.SortedRows(table, values[0], sort == "asc")
            : null;
        var projected = VizProjection.Project(table, columns, order);

        var scaleInput = inputs.Count > 1 ? inputs[1] as TableValue : null;
        if (scaleInput is null)
            return [new TableValue(projected), new TableValue(ColorScale.EmptyTable())];

        var scale = ColorScale.FromTable(scaleInput.Table, context, Kind);
        if (scale is null)
            return [new TableValue(projected), new TableValue(ColorScale.EmptyTable())];

        var colorColumn = table.ColumnIndex(scale.Column);
        if (colorColumn < 0)
        {
            context.Warn($"{Kind}: no column named '{scale.Column}'; using the first value column");
            colorColumn = values.Count > 0 ? values[0] : -1;
        }

        var colored = colorColumn >= 0
            ? AppendColors(context, projected, table, colorColumn, order, scale)
            : projected;

        return [new TableValue(colored), new TableValue(scaleInput.Table)];
    }

    /// <summary>Colours each projected row from `source[colorColumn, row]` (via
    /// `order` when the rows were reordered), dropping any pre-existing r/g/b
    /// columns with a warning first, and warning when bars fall outside the
    /// scale's domain.</summary>
    private static IDataTable AppendColors(IEvalContext context, IDataTable projected,
        IDataTable source, int colorColumn, IReadOnlyList<int>? order, ColorScale scale)
    {
        var n = projected.RowCount();
        var cells = new object?[n];
        for (var i = 0; i < n; i++)
            cells[i] = source[colorColumn, order is null ? i : order[i]];

        var (below, above) = scale.Clamped(cells);
        if (below + above > 0)
        {
            context.Warn($"{Kind}: {below + above} of {n} bars in '{scale.Column}' lie outside "
                + $"the domain and take its end colours ({below} below, {above} above)");
        }

        var r = new double[n];
        var g = new double[n];
        var b = new double[n];
        for (var i = 0; i < n; i++)
        {
            var color = scale.ColorOf(cells[i]);
            r[i] = color.R;
            g[i] = color.G;
            b[i] = color.B;
        }

        var builder = new DataTableBuilder(projected.Name);
        var dropped = new List<string>();
        foreach (var col in projected.Columns)
        {
            if (col.Descriptor.Name is "r" or "g" or "b")
            {
                dropped.Add(col.Descriptor.Name);
                continue;
            }
            builder.AddColumn(((IDataColumnWithValues)col).Values, col.Descriptor.Name, col.Descriptor.Type);
        }
        if (dropped.Count > 0)
        {
            context.Warn($"{Kind}: the projection already has column(s) {string.Join(", ", dropped)}; "
                + "replacing with the scale's colours");
        }

        builder.AddColumn(r, "r");
        builder.AddColumn(g, "g");
        builder.AddColumn(b, "b");
        return builder.Build();
    }
}
