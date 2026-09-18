using Ara3D.DataFlowEngine.Abstractions;
using Ara3D.DataTable;

namespace BimOpenFlow.Nodes.Spatial;

/// <summary>One side of a spatial join: a table, its key column, and each row read
/// as a box (null where the row's coordinates are missing).</summary>
public sealed record Side(IDataTable Table, int KeyIndex, IReadOnlyList<Box?> Boxes)
{
    public object? Key(int row) => Table[KeyIndex, row];
    public string? KeyText(int row) => TableColumns.CellText(Key(row));
    public Type KeyType => Table.Columns[KeyIndex].Descriptor.Type;
    public int RowCount => Boxes.Count;
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

    internal static string TextOr(this ParamValues parameters, string name, string fallback)
        => parameters.GetText(name) is { Length: > 0 } text ? text : fallback;
}
