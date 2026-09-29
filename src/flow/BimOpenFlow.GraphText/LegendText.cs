using Ara3D.DataFlowEngine.Abstractions;
using Ara3D.DataTable;
using BimOpenFlow.Nodes.Support;

namespace BimOpenFlow.GraphText;

/// <summary>Legend tables (Support's ColorScale: view3d.color, view.colormap, chart.bar) and
/// how the colours of a table map back onto one: gradient bins or categories, with counts.</summary>
public static class LegendText
{
    private static readonly string[] Columns = ["column", "domain", "role", "label", "value", "r", "g", "b", "count"];

    /// <summary>Colours this close in RGB (0..1) are the same swatch.</summary>
    public const double ColorTolerance = 1e-6;

    /// <summary>A colour farther than this from every gradient segment is off the scale.</summary>
    public const double OffScaleDistance = 1e-3;

    public static bool IsLegend(IDataTable table)
        => TableText.HasColumns(table, Columns);

    public static ColorScale? Read(IDataTable table)
        => IsLegend(table) ? ColorScale.FromTable(table, SilentContext.Instance, "graphText") : null;

    /// <summary>The first legend among the values, or null.</summary>
    public static ColorScale? Find(IReadOnlyList<FlowValue> values)
        => values.OfType<TableValue>().Select(t => Read(t.Table)).FirstOrDefault(s => s is { Rows.Count: > 0 });

    /// <summary>The legend table's own rows: stops or categories with their value-table counts.</summary>
    public static IReadOnlyList<string> Lines(IDataTable table, DigestContext context)
    {
        if (Read(table) is not { } scale)
            return [];
        if (scale.Rows.Count == 0)
            return ["legend empty"];
        var swatches = scale.Rows
            .Select(r => r.Role is "stop" ? r.Label : $"{r.Role} {Literals.QuoteShort(r.Label, Literals.MaxCellText)} {r.Count}")
            .ToList();
        return [$"legend {Literals.QuoteLine(scale.Column)} {scale.Domain}: {context.List(swatches)}"];
    }

    /// <summary>How many of the given colours fall in each part of the scale: per gradient
    /// segment between neighbouring stops, or per category colour.</summary>
    public static string Bins(ColorScale scale, IReadOnlyList<Rgb> colours, DigestContext context)
        => scale.Domain == "categorical"
            ? CategoryBins(scale, colours, context)
            : GradientBins(scale, colours, context);

    private static string CategoryBins(ColorScale scale, IReadOnlyList<Rgb> colours, DigestContext context)
    {
        var swatches = scale.Rows
            .Where(r => r.Role == "category")
            .GroupBy(r => Key(r.Color))
            .Select(g => (Labels: string.Join("/", g.Select(r => r.Label)), Colour: g.First().Color))
            .ToList();
        var counts = swatches
            .Select(s => $"{Literals.QuoteShort(s.Labels, Literals.MaxCellText)} {colours.Count(c => Same(c, s.Colour))}")
            .ToList();
        var matched = colours.Count(c => swatches.Any(s => Same(c, s.Colour)));
        var rest = colours.Count - matched;
        return $"legend {Literals.QuoteLine(scale.Column)} categorical: {context.List(counts)}"
            + (rest > 0 ? $"; {rest} in no category" : "");
    }

    private static string GradientBins(ColorScale scale, IReadOnlyList<Rgb> colours, DigestContext context)
    {
        var stops = scale.Rows.Where(r => r.Role == "stop").ToList();
        if (stops.Count == 0)
            return $"legend {Literals.QuoteLine(scale.Column)} {scale.Domain}: no stops";
        var range = $"{context.Number(stops[0].Value ?? 0)} to {context.Number(stops[^1].Value ?? 0)}";
        if (stops.Count < 2)
            return $"legend {Literals.QuoteLine(scale.Column)} {scale.Domain} {range}: {colours.Count} in one colour";
        var bins = new int[stops.Count - 1];
        var offScale = 0;
        foreach (var colour in colours)
        {
            var (segment, distance) = NearestSegment(stops, colour);
            if (distance > OffScaleDistance)
                offScale++;
            else
                bins[segment]++;
        }
        return $"legend {Literals.QuoteLine(scale.Column)} {scale.Domain} {range}: bins {string.Join(" / ", bins)}"
            + (offScale > 0 ? $"; {offScale} off the scale" : "");
    }

    private static (int Segment, double Distance) NearestSegment(IReadOnlyList<ColorScale.Row> stops, Rgb c)
    {
        var best = (Segment: 0, Distance: double.MaxValue);
        for (var i = 0; i + 1 < stops.Count; i++)
        {
            var d = DistanceToSegment(stops[i].Color, stops[i + 1].Color, c);
            if (d < best.Distance)
                best = (i, d);
        }
        return best;
    }

    private static double DistanceToSegment(Rgb a, Rgb b, Rgb c)
    {
        var (dr, dg, db) = (b.R - a.R, b.G - a.G, b.B - a.B);
        var length2 = dr * dr + dg * dg + db * db;
        var t = length2 == 0 ? 0 : Math.Clamp(((c.R - a.R) * dr + (c.G - a.G) * dg + (c.B - a.B) * db) / length2, 0, 1);
        var (er, eg, eb) = (a.R + t * dr - c.R, a.G + t * dg - c.G, a.B + t * db - c.B);
        return Math.Sqrt(er * er + eg * eg + eb * eb);
    }

    public static bool Same(Rgb a, Rgb b)
        => Math.Abs(a.R - b.R) <= ColorTolerance && Math.Abs(a.G - b.G) <= ColorTolerance && Math.Abs(a.B - b.B) <= ColorTolerance;

    public static (long, long, long) Key(Rgb c)
        => ((long)Math.Round(c.R / ColorTolerance), (long)Math.Round(c.G / ColorTolerance), (long)Math.Round(c.B / ColorTolerance));

    private sealed class SilentContext : IEvalContext
    {
        public static readonly SilentContext Instance = new();
        public bool IsRun => false;
        public CancellationToken Cancellation => CancellationToken.None;

        public void Warn(string message)
        {
        }
    }
}
