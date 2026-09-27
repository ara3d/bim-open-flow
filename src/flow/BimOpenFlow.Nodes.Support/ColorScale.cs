using System.Globalization;
using Ara3D.DataFlowEngine.Abstractions;
using Ara3D.DataTable;

namespace BimOpenFlow.Nodes.Support;

/// <summary>The colour scale behind a legend table: the column it colours, how its domain was chosen, and its rows.</summary>
public sealed record ColorScale(string Column, string Domain, IReadOnlyList<ColorScale.Row> Rows)
{
    public sealed record Row(string Role, string Label, double? Value, Rgb Color, long? Count);

    public const string TableName = "legend";
    public static readonly Rgb NoValue = new(0.5, 0.5, 0.5);
    public static readonly string[] ColorMapNames = ["viridis", "category10", "redgreen"];

    private static string G4(double value)
        => value.ToString("G4", CultureInfo.InvariantCulture);

    /// <summary>Builds the scale for one column of a values table. A gradient colorMap over a
    /// non-numeric column falls back to category10 with a warning; a manual domain with
    /// min >= max falls back to auto with a warning; values outside a manual domain are
    /// counted in below/above rows and reported in one warning.</summary>
    public static ColorScale Build(IDataTable values, int column, string colorMap,
        bool auto, double min, double max, IEvalContext context, string kind)
    {
        var columnName = values.Columns[column].Descriptor.Name;
        var isNumeric = TableColumns.KindName(values.Columns[column].Descriptor.Type) is "Integer" or "Number";

        if (colorMap != "category10" && !isNumeric)
        {
            context.Warn($"{kind}: colorMap '{colorMap}' needs a numeric value column; using category10");
            colorMap = "category10";
        }

        return colorMap == "category10"
            ? BuildCategorical(values, column, columnName)
            : BuildGradient(values, column, columnName,
                colorMap == "redgreen" ? ColorMaps.RedGreenStops : ColorMaps.ViridisStops,
                auto, min, max, context, kind);
    }

    private static ColorScale BuildGradient(IDataTable values, int column, string columnName,
        IReadOnlyList<Rgb> stops, bool auto, double min, double max, IEvalContext context, string kind)
    {
        var total = values.RowCount();
        var numbers = new double?[total];
        for (var i = 0; i < total; i++)
            numbers[i] = TableColumns.CellNumber(values[column, i]);

        if (!auto && min >= max)
        {
            context.Warn($"{kind}: min ({G4(min)}) is not less than max ({G4(max)}); using the data range instead");
            auto = true;
        }

        string domain;
        double lo, hi;
        if (auto)
        {
            domain = "auto";
            var present = numbers.Where(n => n.HasValue).Select(n => n!.Value).ToList();
            lo = present.Count > 0 ? present.Min() : 0;
            hi = present.Count > 0 ? present.Max() : 0;
        }
        else
        {
            domain = "manual";
            lo = min;
            hi = max;
        }

        var stopRows = new List<Row>();
        if (lo >= hi)
        {
            stopRows.Add(new Row("stop", G4(lo), lo, ColorMaps.Gradient(stops, 0.5), null));
        }
        else
        {
            for (var i = 0; i < stops.Count; i++)
            {
                var t = (double)i / (stops.Count - 1);
                var v = lo + t * (hi - lo);
                stopRows.Add(new Row("stop", G4(v), v, stops[i], null));
            }
        }

        long below = 0, above = 0, missing = 0;
        foreach (var n in numbers)
        {
            if (n is null) { missing++; continue; }
            if (n.Value < lo) below++;
            else if (n.Value > hi) above++;
        }

        if (domain == "manual" && below + above > 0)
        {
            context.Warn($"{kind}: {below + above} of {total} values in '{columnName}' lie outside the manual domain "
                + $"{G4(lo)}..{G4(hi)} and take its end colours ({below} below, {above} above)");
        }

        var rows = new List<Row>(stopRows);
        if (below > 0)
            rows.Add(new Row("below", $"< {G4(lo)}", lo, stopRows[0].Color, below));
        if (above > 0)
            rows.Add(new Row("above", $"> {G4(hi)}", hi, stopRows[^1].Color, above));
        if (missing > 0)
            rows.Add(new Row("missing", "no value", null, NoValue, missing));

        return new ColorScale(columnName, domain, rows);
    }

    private static ColorScale BuildCategorical(IDataTable values, int column, string columnName)
    {
        var total = values.RowCount();
        var counts = new Dictionary<string, long>(StringComparer.Ordinal);
        long missing = 0;
        for (var i = 0; i < total; i++)
        {
            var text = TableColumns.CellText(values[column, i]);
            if (text is null) { missing++; continue; }
            counts[text] = counts.GetValueOrDefault(text) + 1;
        }

        var ordered = counts.Keys.OrderBy(text => text, StringComparer.Ordinal).ToList();
        var rows = new List<Row>();
        for (var i = 0; i < ordered.Count; i++)
        {
            var text = ordered[i];
            rows.Add(new Row("category", text, null, ColorMaps.Categorical(i), counts[text]));
        }
        if (missing > 0)
            rows.Add(new Row("missing", "no value", null, NoValue, missing));

        return new ColorScale(columnName, "categorical", rows);
    }

    /// <summary>Gradient: piecewise-linear between stop rows, clamped to the end stops
    /// (equal to ColorMaps.Gradient over the domain). Categorical: the category row whose
    /// label equals CellText(cell). Anything else: NoValue.</summary>
    public Rgb ColorOf(object? cell)
    {
        if (Domain == "categorical")
        {
            var text = TableColumns.CellText(cell);
            if (text is null)
                return NoValue;
            foreach (var row in Rows)
                if (row.Role == "category" && row.Label == text)
                    return row.Color;
            return NoValue;
        }

        var stops = Rows.Where(r => r.Role == "stop").ToList();
        if (stops.Count == 0)
            return NoValue;

        var value = TableColumns.CellNumber(cell);
        if (value is null)
            return NoValue;

        if (stops.Count == 1)
            return stops[0].Color;

        var lo = stops[0].Value!.Value;
        var hi = stops[^1].Value!.Value;
        var t = hi > lo ? (value.Value - lo) / (hi - lo) : 0.5;
        t = double.IsNaN(t) ? 0 : Math.Clamp(t, 0, 1);
        var scaled = t * (stops.Count - 1);
        var floor = (int)Math.Floor(scaled);
        if (floor >= stops.Count - 1)
            return stops[^1].Color;
        var f = scaled - floor;
        var a = stops[floor].Color;
        var b = stops[floor + 1].Color;
        return new Rgb(a.R + (b.R - a.R) * f, a.G + (b.G - a.G) * f, a.B + (b.B - a.B) * f);
    }

    /// <summary>Numeric cells below the first stop and above the last; (0, 0) for categorical scales.</summary>
    public (long Below, long Above) Clamped(IEnumerable<object?> cells)
    {
        if (Domain == "categorical")
            return (0, 0);

        var stops = Rows.Where(r => r.Role == "stop").ToList();
        if (stops.Count == 0)
            return (0, 0);

        var lo = stops[0].Value!.Value;
        var hi = stops[^1].Value!.Value;
        long below = 0, above = 0;
        foreach (var cell in cells)
        {
            var value = TableColumns.CellNumber(cell);
            if (value is null)
                continue;
            if (value.Value < lo)
                below++;
            else if (value.Value > hi)
                above++;
        }
        return (below, above);
    }

    public IDataTable ToTable()
    {
        var n = Rows.Count;
        var columnCells = new object?[n];
        var domainCells = new object?[n];
        var roleCells = new object?[n];
        var labelCells = new object?[n];
        var valueCells = new object?[n];
        var rCells = new object?[n];
        var gCells = new object?[n];
        var bCells = new object?[n];
        var countCells = new object?[n];
        for (var i = 0; i < n; i++)
        {
            var row = Rows[i];
            columnCells[i] = Column;
            domainCells[i] = Domain;
            roleCells[i] = row.Role;
            labelCells[i] = row.Label;
            valueCells[i] = row.Value;
            rCells[i] = row.Color.R;
            gCells[i] = row.Color.G;
            bCells[i] = row.Color.B;
            countCells[i] = row.Count;
        }

        return new MemoryTable(TableName,
        [
            new MemoryColumn("column", typeof(string), columnCells, 0),
            new MemoryColumn("domain", typeof(string), domainCells, 1),
            new MemoryColumn("role", typeof(string), roleCells, 2),
            new MemoryColumn("label", typeof(string), labelCells, 3),
            new MemoryColumn("value", typeof(double?), valueCells, 4),
            new MemoryColumn("r", typeof(double), rCells, 5),
            new MemoryColumn("g", typeof(double), gCells, 6),
            new MemoryColumn("b", typeof(double), bCells, 7),
            new MemoryColumn("count", typeof(long?), countCells, 8),
        ]);
    }

    /// <summary>Reads a legend table; null plus a warning when a required column is missing.</summary>
    public static ColorScale? FromTable(IDataTable table, IEvalContext context, string kind)
    {
        string[] required = ["column", "domain", "role", "label", "value", "r", "g", "b", "count"];
        foreach (var name in required)
        {
            if (table.ColumnIndex(name) >= 0)
                continue;
            context.Warn($"{kind}: legend table is missing column '{name}'");
            return null;
        }

        var columnIdx = table.ColumnIndex("column");
        var domainIdx = table.ColumnIndex("domain");
        var roleIdx = table.ColumnIndex("role");
        var labelIdx = table.ColumnIndex("label");
        var valueIdx = table.ColumnIndex("value");
        var rIdx = table.ColumnIndex("r");
        var gIdx = table.ColumnIndex("g");
        var bIdx = table.ColumnIndex("b");
        var countIdx = table.ColumnIndex("count");

        var n = table.RowCount();
        var rows = new List<Row>(n);
        for (var i = 0; i < n; i++)
        {
            var color = new Rgb(
                TableColumns.CellNumber(table[rIdx, i]) ?? 0,
                TableColumns.CellNumber(table[gIdx, i]) ?? 0,
                TableColumns.CellNumber(table[bIdx, i]) ?? 0);
            var count = TableColumns.CellNumber(table[countIdx, i]) is { } countValue
                ? (long)countValue
                : (long?)null;
            rows.Add(new Row(
                TableColumns.CellText(table[roleIdx, i]) ?? "",
                TableColumns.CellText(table[labelIdx, i]) ?? "",
                TableColumns.CellNumber(table[valueIdx, i]),
                color, count));
        }

        var column = n > 0 ? TableColumns.CellText(table[columnIdx, 0]) ?? "" : "";
        var domain = n > 0 ? TableColumns.CellText(table[domainIdx, 0]) ?? "auto" : "auto";
        return new ColorScale(column, domain, rows);
    }

    /// <summary>The legend schema with zero rows: what a consumer emits with no scale.</summary>
    public static IDataTable EmptyTable()
        => new ColorScale("", "auto", []).ToTable();
}
