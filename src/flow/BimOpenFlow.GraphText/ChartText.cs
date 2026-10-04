using Ara3D.DataTable;
using BimOpenFlow.Nodes.Support;

namespace BimOpenFlow.GraphText;

/// <summary>The chart digest of a chart.* node's table output: the first column holds the
/// categories (chart.bar's label, chart.line's x), the numeric columns other than the bar
/// colours r g b are the series.</summary>
public static class ChartText
{
    public const string KindPrefix = "chart.";
    public const string Port = "table";
    private static readonly string[] BarColours = ["r", "g", "b"];

    public static bool IsChart(IDataTable table, DigestContext context)
        => context.Node.Kind.StartsWith(KindPrefix, StringComparison.Ordinal)
           && context.Port == Port
           && table.Columns.Count > 0;

    public static IReadOnlyList<string> Lines(IDataTable table, DigestContext context)
    {
        if (!IsChart(table, context))
            return [];
        var series = Enumerable.Range(1, table.Columns.Count - 1)
            .Where(i => TableColumns.KindName(table.Columns[i].Descriptor.Type) is "Integer" or "Number")
            .Where(i => !BarColours.Contains(table.Columns[i].Descriptor.Name, StringComparer.OrdinalIgnoreCase))
            .ToList();
        var rows = table.RowCount();
        var head = $"chart {rows} categories x {series.Count} series: "
            + string.Join(", ", series.Select(i => Literals.QuoteLine(table.Columns[i].Descriptor.Name)));
        var points = Enumerable.Range(0, Math.Min(rows, context.Options.MaxListed))
            .Select(row => $"{Literals.Cell(table[0, row], context.Mode)}: "
                + string.Join(", ", series.Select(i => Literals.Cell(table[i, row], context.Mode))))
            .ToList();
        return rows > context.Options.MaxListed
            ? [head, .. points, $"(+{rows - context.Options.MaxListed} more categories)"]
            : [head, .. points];
    }
}
