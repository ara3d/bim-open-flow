using Ara3D.DataFlowEngine.Abstractions;
using Ara3D.DataTable;
using BimOpenFlow.Nodes.Support;

namespace BimOpenFlow.GraphText;

/// <summary>The scene digest of a table the 3D pane draws: an instance or boxes table,
/// recognised by its world-bounds columns (Nodes.Geometry README). Counts come from the
/// columns the viewer reads: `a` (0 hides, below 1 fades), `r g b` (ColorScale.NoValue is
/// the grey of an unmatched row), and `offsetX..Z`.</summary>
public static class SceneText
{
    private static readonly string[] Bounds = ["minX", "minY", "minZ", "maxX", "maxY", "maxZ"];
    private static readonly string[] Offsets = ["offsetX", "offsetY", "offsetZ"];

    public static bool IsScene(IDataTable table)
        => TableText.HasColumns(table, Bounds);

    public static IReadOnlyList<string> Lines(IDataTable table, DigestContext context)
        => IsScene(table)
            ? [Counts(table, context), .. Colours(table, context), .. OffsetLine(table), BoundsLine(table, context)]
            : [];

    private static string Noun(IDataTable table)
        => table.ColumnIndex("instanceIndex") >= 0 || table.ColumnIndex("meshId") >= 0 ? "instances" : "boxes";

    private static string Counts(IDataTable table, DigestContext context)
    {
        var rows = table.RowCount();
        var alpha = table.ColumnIndex("a") is var a and >= 0 ? TableText.Numbers(table, a) : new double?[rows];
        var hidden = alpha.Count(v => v <= 0);
        var faded = alpha.Count(v => v is > 0 and < 1);
        var upstream = context.Inputs.OfType<TableValue>().Select(t => t.Table).FirstOrDefault(IsScene);
        var from = upstream is not null && upstream.RowCount() != rows
            ? $" (of {upstream.RowCount()} upstream)"
            : "";
        return $"scene {rows} {Noun(table)}{from}: {rows - hidden - faded} opaque, {faded} faded, {hidden} hidden";
    }

    private static IReadOnlyList<string> Colours(IDataTable table, DigestContext context)
    {
        if (!TableText.HasColumns(table, "r", "g", "b"))
            return ["no colour columns"];
        var colours = RowColours(table);
        var coloured = colours.Where(c => !LegendText.Same(c, ColorScale.NoValue)).ToList();
        var distinct = coloured.Select(LegendText.Key).Distinct().Count();
        var line = $"{coloured.Count} coloured in {distinct} colours, {colours.Count - coloured.Count} grey";
        return LegendText.Find([.. context.Outputs, .. context.Inputs]) is { } scale
            ? [line, LegendText.Bins(scale, coloured, context)]
            : [line];
    }

    private static IReadOnlyList<Rgb> RowColours(IDataTable table)
    {
        var r = TableText.Numbers(table, table.ColumnIndex("r"));
        var g = TableText.Numbers(table, table.ColumnIndex("g"));
        var b = TableText.Numbers(table, table.ColumnIndex("b"));
        var colours = new Rgb[r.Length];
        for (var i = 0; i < colours.Length; i++)
            colours[i] = r[i] is null || g[i] is null || b[i] is null
                ? ColorScale.NoValue
                : new Rgb(r[i]!.Value, g[i]!.Value, b[i]!.Value);
        return colours;
    }

    private static IReadOnlyList<string> OffsetLine(IDataTable table)
    {
        var columns = Offsets.Select(table.ColumnIndex).Where(i => i >= 0).Select(i => TableText.Numbers(table, i)).ToList();
        if (columns.Count == 0)
            return [];
        var moved = Enumerable.Range(0, table.RowCount()).Count(row => columns.Any(c => c[row] is { } v && v != 0));
        return [$"offsets move {moved} {Noun(table)}"];
    }

    private static string BoundsLine(IDataTable table, DigestContext context)
    {
        var extremes = Bounds
            .Select((name, i) => TableText.Numbers(table, table.ColumnIndex(name))
                .Where(v => v is { } x && double.IsFinite(x))
                .Select(v => v!.Value)
                .DefaultIfEmpty(double.NaN)
                .Aggregate(i < 3 ? Math.Min : (Func<double, double, double>)Math.Max))
            .Select(context.Number)
            .ToList();
        return $"bounds ({extremes[0]}, {extremes[1]}, {extremes[2]}) to ({extremes[3]}, {extremes[4]}, {extremes[5]})";
    }
}
