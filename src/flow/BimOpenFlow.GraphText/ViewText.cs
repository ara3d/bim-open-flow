using Ara3D.DataTable;
using BimOpenFlow.Nodes.Support;

namespace BimOpenFlow.GraphText;

/// <summary>The small view tables the 3D pane reads besides instances: a view recipe
/// (`operation`, `input` JSON; view3d.scene and its chain: sections, section boxes,
/// projection, environment) and a camera (view3d.camera).</summary>
public static class ViewText
{
    private static readonly string[] Recipe = ["operation", "input"];
    private static readonly string[] Camera = ["name", "posX", "posY", "posZ", "targetX", "targetY", "targetZ"];

    public static IReadOnlyList<string> Lines(IDataTable table, DigestContext context)
        => [.. RecipeLines(table, context), .. CameraLines(table, context)];

    private static IReadOnlyList<string> RecipeLines(IDataTable table, DigestContext context)
    {
        if (!TableText.HasColumns(table, Recipe))
            return [];
        var operation = table.ColumnIndex("operation");
        var input = table.ColumnIndex("input");
        return Enumerable.Range(0, Math.Min(table.RowCount(), context.Options.MaxListed))
            .Select(row => $"view {TableColumns.CellText(table[operation, row])} {TableColumns.CellText(table[input, row])}")
            .ToList();
    }

    private static IReadOnlyList<string> CameraLines(IDataTable table, DigestContext context)
    {
        if (!TableText.HasColumns(table, Camera))
            return [];
        var columns = Camera.Select(table.ColumnIndex).ToList();
        string Point(int row, int first)
            => $"({string.Join(", ", columns.Skip(first).Take(3).Select(c => Literals.Cell(table[c, row], context.Mode)))})";
        return Enumerable.Range(0, Math.Min(table.RowCount(), context.Options.MaxListed))
            .Select(row => $"camera {Literals.Cell(table[columns[0], row], context.Mode)} at {Point(row, 1)} looking at {Point(row, 4)}")
            .ToList();
    }
}
