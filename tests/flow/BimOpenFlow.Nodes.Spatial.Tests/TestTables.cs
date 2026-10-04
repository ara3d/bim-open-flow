using Ara3D.DataFlowEngine.Abstractions;

namespace BimOpenFlow.Nodes.Spatial.Tests;

/// <summary>Box and point fixtures in the pack's column conventions.</summary>
internal static class TestTables
{
    public static TableValue Boxes(params (string Name, double MinX, double MinY, double MinZ, double MaxX, double MaxY, double MaxZ)[] rows)
        => NodeTestHelpers.Table(
            ("Name", rows.Select(r => r.Name).ToArray()),
            ("MinX", rows.Select(r => r.MinX).ToArray()),
            ("MinY", rows.Select(r => r.MinY).ToArray()),
            ("MinZ", rows.Select(r => r.MinZ).ToArray()),
            ("MaxX", rows.Select(r => r.MaxX).ToArray()),
            ("MaxY", rows.Select(r => r.MaxY).ToArray()),
            ("MaxZ", rows.Select(r => r.MaxZ).ToArray()));

    public static TableValue Points(params (string Name, double X, double Y, double Z)[] rows)
        => NodeTestHelpers.Table(
            ("Name", rows.Select(r => r.Name).ToArray()),
            ("CenterX", rows.Select(r => r.X).ToArray()),
            ("CenterY", rows.Select(r => r.Y).ToArray()),
            ("CenterZ", rows.Select(r => r.Z).ToArray()));

    /// <summary>A unit cube with its minimum corner at (x, y, z).</summary>
    public static (string, double, double, double, double, double, double) Cube(string name, double x, double y, double z, double size = 1)
        => (name, x, y, z, x + size, y + size, z + size);

    public static IReadOnlyList<(string? A, string? B)> KeyPairs(this Ara3D.DataTable.IDataTable pairs)
        => Enumerable.Range(0, pairs.Rows.Count)
            .Select(r => (pairs.Cell("A", r)?.ToString(), pairs.Cell("B", r)?.ToString()))
            .ToList();
}
