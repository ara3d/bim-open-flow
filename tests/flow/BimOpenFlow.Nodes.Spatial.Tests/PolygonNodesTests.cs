using Ara3D.DataFlowEngine.Abstractions;

namespace BimOpenFlow.Nodes.Spatial.Tests;

[TestFixture]
public class PolygonNodesTests
{
    private static TableValue Polygons(params (string Name, string Footprint)[] rows)
        => NodeTestHelpers.Table(
            ("Name", rows.Select(r => r.Name).ToArray()),
            ("Footprint", rows.Select(r => r.Footprint).ToArray()));

    private const string Square = "POLYGON((0 0, 4 0, 4 4, 0 4, 0 0))";
    private const string LShape = "POLYGON((0 0, 4 0, 4 2, 2 2, 2 4, 0 4, 0 0))";
    private const string Far = "POLYGON((10 10, 12 10, 12 12, 10 12, 10 10))";

    [Test]
    public void Footprint_Writes_The_Box_Rectangle()
    {
        var boxes = TestTables.Boxes(("room", 1, 2, 0, 5, 6, 3));
        var table = new FootprintNode().EvalTable([boxes]);
        Assert.That(table.ColumnNames(), Does.Contain("Footprint"));
        Assert.That(table.Cell("Footprint", 0), Is.EqualTo("POLYGON((1 2, 5 2, 5 6, 1 6, 1 2))"));
    }

    [Test]
    public void Footprint_Errors_On_An_Existing_Column_And_On_Non_Box_Input()
    {
        var boxes = TestTables.Boxes(("room", 1, 2, 0, 5, 6, 3));
        Assert.That(() => new FootprintNode().EvalTable([boxes], ("as", "Name")),
            Throws.ArgumentException.With.Message.Contains("'Name'"));
        Assert.That(() => new FootprintNode().EvalTable([TestTables.Points(("p", 0, 0, 0))]),
            Throws.ArgumentException.With.Message.Contains("MinX..MaxZ"));
    }

    [Test]
    public void Polygon_Measures_A_Square_And_An_L_Shape()
    {
        var table = new PolygonNode().EvalTable([Polygons(("sq", Square), ("l", LShape))]);
        Assert.That(table.ColumnNames(), Is.EqualTo(new[] { "Name", "Footprint", "Area", "Perimeter", "CentroidX", "CentroidY", "Vertices", "IsConvex" }));
        Assert.That((double)table.Cell("Area", 0)!, Is.EqualTo(16).Within(1e-9));
        Assert.That((double)table.Cell("Perimeter", 0)!, Is.EqualTo(16).Within(1e-9));
        Assert.That(table.Cell("CentroidX", 0), Is.EqualTo(2.0));
        Assert.That(table.Cell("Vertices", 0), Is.EqualTo(4L));
        Assert.That(table.Cell("IsConvex", 0), Is.EqualTo(true));
        Assert.That((double)table.Cell("Area", 1)!, Is.EqualTo(12).Within(1e-9));
        Assert.That((double)table.Cell("Perimeter", 1)!, Is.EqualTo(16).Within(1e-9));
        Assert.That(table.Cell("IsConvex", 1), Is.EqualTo(false));
        // Area centroid of the L: (4*2*(2,1) + 2*2*(1,3)) / 12.
        Assert.That((double)table.Cell("CentroidX", 1)!, Is.EqualTo(20.0 / 12).Within(1e-9));
        Assert.That((double)table.Cell("CentroidY", 1)!, Is.EqualTo(20.0 / 12).Within(1e-9));
    }

    [Test]
    public void Polygon_Precision_Survives_Site_Coordinates()
    {
        var text = "POLYGON((500000.25 4000000.5, 500004.25 4000000.5, 500004.25 4000003.5, 500000.25 4000003.5, 500000.25 4000000.5))";
        var table = new PolygonNode().EvalTable([Polygons(("far", text))]);
        Assert.That((double)table.Cell("Area", 0)!, Is.EqualTo(12).Within(1e-6));
        Assert.That((double)table.Cell("CentroidX", 0)!, Is.EqualTo(500002.25).Within(1e-6));
    }

    [Test]
    public void Polygon_Nulls_Empty_Cells_And_Warns_About_Holes()
    {
        var input = NodeTestHelpers.Table(
            ("Name", new[] { "empty", "holed" }),
            ("Footprint", new object?[] { null, "POLYGON((0 0, 4 0, 4 4, 0 4), (1 1, 2 1, 2 2))" }));
        var (table, warnings) = new PolygonNode().EvalWithWarnings([input]);
        Assert.That(table.Cell("Area", 0), Is.Null);
        Assert.That((double)table.Cell("Area", 1)!, Is.EqualTo(16).Within(1e-9));
        Assert.That(warnings, Has.One.Contains("1 hole(s)"));
    }

    [Test]
    public void Polygon_Errors_On_Bad_Wkt_Naming_The_Row()
        => Assert.That(() => new PolygonNode().EvalTable([Polygons(("ok", Square), ("bad", "LINESTRING(0 0, 1 1)"))]),
            Throws.ArgumentException.With.Message.Contains("row 1"));

    [Test]
    public void PolygonContains_Finds_The_Smallest_Polygon_Around_A_Point()
    {
        var points = TestTables.Points(("inL", 1, 1, 9), ("notch", 3, 3, 9), ("edge", 4, 1, 9), ("out", 20, 20, 9));
        var table = new PolygonContainsNode().EvalTable([points, Polygons(("sq", Square), ("l", LShape), ("far", Far))]);
        Assert.That(table.ColumnNames(), Is.EqualTo(new[] { "A", "B", "Area" }));
        Assert.That(table.KeyPairs(), Is.EqualTo(new[] { ("inL", "l"), ("notch", "sq"), ("edge", "l") }));
        Assert.That((double)table.Cell("Area", 0)!, Is.EqualTo(12).Within(1e-9));
    }

    [Test]
    public void PolygonContains_Lists_All_Containers_When_Not_Smallest_And_Uses_Box_Centers()
    {
        var boxes = TestTables.Boxes(("col", 0.5, 0.5, 0, 1.5, 1.5, 3));
        var table = new PolygonContainsNode().EvalTable([boxes, Polygons(("sq", Square), ("l", LShape))], ("smallest", "false"));
        Assert.That(table.KeyPairs(), Is.EqualTo(new[] { ("col", "sq"), ("col", "l") }));
    }

    [Test]
    public void PolygonIntersects_Detects_Crossing_Touching_And_Nesting()
    {
        var a = Polygons(
            ("cross", "POLYGON((3 -1, 5 -1, 5 5, 3 5, 3 -1))"),
            ("touch", "POLYGON((4 0, 6 0, 6 1, 4 1, 4 0))"),
            ("inside", "POLYGON((1 1, 2 1, 2 2, 1 2, 1 1))"),
            ("around", "POLYGON((-1 -1, 9 -1, 9 9, -1 9, -1 -1))"),
            ("apart", "POLYGON((7 0, 8 0, 8 1, 7 1, 7 0))"));
        var table = new PolygonIntersectsNode().EvalTable([a, Polygons(("sq", Square))]);
        Assert.That(table.ColumnNames(), Is.EqualTo(new[] { "A", "B" }));
        Assert.That(table.KeyPairs(), Is.EqualTo(new[] { ("cross", "sq"), ("touch", "sq"), ("inside", "sq"), ("around", "sq") }));
    }

    [Test]
    public void PolygonIntersects_Self_Join_Skips_Itself()
    {
        var polys = Polygons(("sq", Square), ("l", LShape), ("far", Far));
        var table = new PolygonIntersectsNode().EvalTable([polys, polys]);
        Assert.That(table.KeyPairs(), Is.EqualTo(new[] { ("sq", "l"), ("l", "sq") }));
    }

    [Test]
    public void Concave_Bounds_Overlap_Is_Not_Enough()
    {
        // The notch of the L is inside its bounds but outside the polygon.
        var notch = Polygons(("notch", "POLYGON((2.5 2.5, 3.5 2.5, 3.5 3.5, 2.5 3.5, 2.5 2.5))"));
        var table = new PolygonIntersectsNode().EvalTable([notch, Polygons(("l", LShape))]);
        Assert.That(table.Rows.Count, Is.EqualTo(0));
    }
}
