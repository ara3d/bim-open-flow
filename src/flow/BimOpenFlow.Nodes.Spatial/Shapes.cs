using Ara3D.DataFlowEngine.Abstractions;
using Ara3D.DataTable;

namespace BimOpenFlow.Nodes.Spatial;

/// <summary>One side of a spatial join as the pairs table sees it: a table and the
/// column whose cells become the pair's A or B key.</summary>
public interface IKeyedSide
{
    IDataTable Table { get; }
    int KeyIndex { get; }
}

public static class KeyedSides
{
    public static object? Key(this IKeyedSide side, int row) => side.Table[side.KeyIndex, row];
    public static string? KeyText(this IKeyedSide side, int row) => TableColumns.CellText(side.Key(row));
    public static Type KeyType(this IKeyedSide side) => side.Table.Columns[side.KeyIndex].Descriptor.Type;
}

/// <summary>A side read as boxes (null where the row's coordinates are missing).</summary>
public sealed record Side(IDataTable Table, int KeyIndex, IReadOnlyList<Box?> Boxes) : IKeyedSide
{
    public int RowCount => Boxes.Count;
}

/// <summary>A side read as plan polygons from a WKT column (null where the cell is empty).</summary>
public sealed record PolygonSide(IDataTable Table, int KeyIndex, IReadOnlyList<Polygon?> Polygons) : IKeyedSide
{
    public int RowCount => Polygons.Count;
}

/// <summary>Reads the pack's shape conventions out of tables: a box from the six
/// MinX..MaxZ columns when all are present, otherwise a point from the columns the
/// x, y, z params name. Nodes share the param specs so every side reads alike.</summary>
public static class Shapes
{
    public const string X = "x";
    public const string Y = "y";
    public const string Z = "z";

    public static IReadOnlyList<ParamSpec> PointParams(string suggestFrom) =>
    [
        new(X, ParamKind.Text, SpatialColumns.CenterX, Suggest: SuggestSource.ColumnsOf(suggestFrom)),
        new(Y, ParamKind.Text, SpatialColumns.CenterY, Suggest: SuggestSource.ColumnsOf(suggestFrom)),
        new(Z, ParamKind.Text, SpatialColumns.CenterZ, Suggest: SuggestSource.ColumnsOf(suggestFrom)),
    ];

    public static ParamSpec KeyParam(string name, string suggestFrom)
        => new(name, ParamKind.Text, SpatialColumns.Name, Suggest: SuggestSource.ColumnsOf(suggestFrom));

    public static bool HasBoxColumns(IDataTable table)
        => SpatialColumns.BoxColumns.All(c => table.ColumnIndex(c) >= 0);

    /// <summary>The table read as boxes or points, keyed by the named column. A row
    /// with a missing coordinate reads as null; a box with Min above Max or a
    /// non-finite coordinate is an error naming the row.</summary>
    public static Side Read(IDataTable table, string kind, string keyColumn, ParamValues parameters, string label)
    {
        var keyIndex = table.RequireColumn(keyColumn, kind);
        var boxes = HasBoxColumns(table)
            ? ReadBoxes(table, kind, label)
            : ReadPoints(table, kind, parameters, label);
        return new(table, keyIndex, boxes);
    }

    private static IReadOnlyList<Box?> ReadBoxes(IDataTable table, string kind, string label)
    {
        var c = SpatialColumns.BoxColumns.Select(n => table.ColumnIndex(n)).ToArray();
        var rows = table.RowCount();
        var boxes = new Box?[rows];
        for (var row = 0; row < rows; row++)
        {
            var v = c.Select(i => TableColumns.CellNumber(table[i, row])).ToArray();
            if (v.Any(n => n == null)) continue;
            var box = new Box(v[0]!.Value, v[1]!.Value, v[2]!.Value, v[3]!.Value, v[4]!.Value, v[5]!.Value);
            boxes[row] = box.IsValid
                ? box
                : throw new ArgumentException($"{kind}: row {row} of input '{label}' is not a valid box (Min must not exceed Max).");
        }
        return boxes;
    }

    private static IReadOnlyList<Box?> ReadPoints(IDataTable table, string kind, ParamValues parameters, string label)
    {
        int Column(string param, string fallback)
        {
            var name = parameters.TextOr(param, fallback);
            return table.ColumnIndex(name) is var i && i >= 0
                ? i
                : throw new ArgumentException(
                    $"{kind}: input '{label}' has neither box columns (MinX..MaxZ) nor a point column named '{name}'.");
        }
        var xi = Column(X, SpatialColumns.CenterX);
        var yi = Column(Y, SpatialColumns.CenterY);
        var zi = Column(Z, SpatialColumns.CenterZ);
        var rows = table.RowCount();
        var boxes = new Box?[rows];
        for (var row = 0; row < rows; row++)
        {
            var x = TableColumns.CellNumber(table[xi, row]);
            var y = TableColumns.CellNumber(table[yi, row]);
            var z = TableColumns.CellNumber(table[zi, row]);
            if (x == null || y == null || z == null) continue;
            var point = Box.Point(x.Value, y.Value, z.Value);
            boxes[row] = point.IsValid
                ? point
                : throw new ArgumentException($"{kind}: row {row} of input '{label}' has a non-finite coordinate.");
        }
        return boxes;
    }

    public const string PolygonParam = "polygon";

    public static ParamSpec PolygonColumnParam(string suggestFrom)
        => new(PolygonParam, ParamKind.Text, SpatialColumns.Footprint, Suggest: SuggestSource.ColumnsOf(suggestFrom));

    /// <summary>The table's polygon column parsed as WKT, keyed by the named column. An
    /// empty cell reads as null; unparsable text is an error naming the row. Holes are
    /// dropped, with one warning per table.</summary>
    public static PolygonSide ReadPolygons(IDataTable table, string kind, string keyColumn, string polygonColumn,
        string label, IEvalContext context)
    {
        var keyIndex = table.RequireColumn(keyColumn, kind);
        var polygons = ParsePolygons(table, kind, polygonColumn, label, context);
        return new(table, keyIndex, polygons);
    }

    public static IReadOnlyList<Polygon?> ParsePolygons(IDataTable table, string kind, string polygonColumn,
        string label, IEvalContext context)
    {
        var column = table.RequireColumn(polygonColumn, kind);
        var rows = table.RowCount();
        var polygons = new Polygon?[rows];
        var holes = 0;
        for (var row = 0; row < rows; row++)
        {
            var text = TableColumns.CellText(table[column, row]);
            if (string.IsNullOrWhiteSpace(text)) continue;
            if (!Wkt.TryParsePolygon(text, out var parsed))
                throw new ArgumentException($"{kind}: row {row} of input '{label}' is not a WKT POLYGON.");
            holes += parsed.Holes;
            polygons[row] = new Polygon(parsed.Outer);
        }
        if (holes > 0)
            context.Warn($"{kind}: {holes} hole(s) in input '{label}' were ignored; only outer rings are used.");
        return polygons;
    }

    internal static string TextOr(this ParamValues parameters, string name, string fallback)
        => parameters.GetText(name) is { Length: > 0 } text ? text : fallback;
}
