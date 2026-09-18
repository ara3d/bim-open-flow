namespace BimOpenFlow.Nodes.Spatial.Tests;

[TestFixture]
public class IntersectsNodeTests
{
    private static readonly IntersectsNode Node = new();

    [Test]
    public void Overlapping_Boxes_Pair_With_Their_Overlap_Volume()
    {
        var a = TestTables.Boxes(TestTables.Cube("duct", 0, 0, 0, 2));
        var b = TestTables.Boxes(TestTables.Cube("beam", 1, 1, 1, 2), TestTables.Cube("far", 10, 10, 10));
        var table = Node.EvalTable([a, b]);
        Assert.That(table.ColumnNames(), Is.EqualTo(new[] { "A", "B", "OverlapVolume" }));
        Assert.That(table.KeyPairs(), Is.EqualTo(new[] { ("duct", "beam") }));
        Assert.That(table.Cell("OverlapVolume", 0), Is.EqualTo(1.0));
    }

    [Test]
    public void Touching_Boxes_Intersect_With_Zero_Volume()
    {
        var a = TestTables.Boxes(TestTables.Cube("x", 0, 0, 0));
        var b = TestTables.Boxes(TestTables.Cube("y", 1, 0, 0));
        var table = Node.EvalTable([a, b]);
        Assert.That(table.KeyPairs(), Is.EqualTo(new[] { ("x", "y") }));
        Assert.That(table.Cell("OverlapVolume", 0), Is.EqualTo(0.0));
    }

    [Test]
    public void Crossing_Boxes_With_No_Corner_Inside_Each_Other_Still_Intersect()
    {
        var a = TestTables.Boxes(("plus", -5, -1, 0, 5, 1, 1));
        var b = TestTables.Boxes(("bar", -1, -5, 0, 1, 5, 1));
        Assert.That(Node.EvalTable([a, b]).KeyPairs(), Is.EqualTo(new[] { ("plus", "bar") }));
    }

    [Test]
    public void Self_Join_Skips_Equal_Keys_And_Lists_Both_Directions()
    {
        var boxes = TestTables.Boxes(TestTables.Cube("p", 0, 0, 0, 2), TestTables.Cube("q", 1, 1, 1, 2), TestTables.Cube("r", 9, 9, 9));
        var table = Node.EvalTable([boxes, boxes]);
        Assert.That(table.KeyPairs(), Is.EqualTo(new[] { ("p", "q"), ("q", "p") }));
    }

    [Test]
    public void ExcludeSelf_False_Keeps_Every_Row_Against_Itself()
    {
        var boxes = TestTables.Boxes(TestTables.Cube("p", 0, 0, 0), TestTables.Cube("r", 9, 9, 9));
        var table = Node.EvalTable([boxes, boxes], ("excludeSelf", "false"));
        Assert.That(table.KeyPairs(), Is.EqualTo(new[] { ("p", "p"), ("r", "r") }));
    }

    [Test]
    public void Points_Are_Degenerate_Boxes()
    {
        var points = TestTables.Points(("inside", 0.5, 0.5, 0.5), ("outside", 5, 5, 5));
        var boxes = TestTables.Boxes(TestTables.Cube("room", 0, 0, 0));
        Assert.That(Node.EvalTable([points, boxes]).KeyPairs(), Is.EqualTo(new[] { ("inside", "room") }));
    }

    [Test]
    public void Rows_With_Missing_Coordinates_Never_Match()
    {
        var a = NodeTestHelpers.Table(
            ("Name", new[] { "gap", "ok" }),
            ("MinX", new object?[] { null, 0.0 }), ("MinY", new object?[] { 0.0, 0.0 }), ("MinZ", new object?[] { 0.0, 0.0 }),
            ("MaxX", new object?[] { 1.0, 1.0 }), ("MaxY", new object?[] { 1.0, 1.0 }), ("MaxZ", new object?[] { 1.0, 1.0 }));
        var b = TestTables.Boxes(TestTables.Cube("cube", 0, 0, 0));
        Assert.That(Node.EvalTable([a, b]).KeyPairs(), Is.EqualTo(new[] { ("ok", "cube") }));
    }

    [Test]
    public void Keys_Keep_Their_Source_Column_Type()
    {
        var a = NodeTestHelpers.Table(
            ("EntityIndex", new[] { 7L }), ("CenterX", new[] { 0.5 }), ("CenterY", new[] { 0.5 }), ("CenterZ", new[] { 0.5 }));
        var b = TestTables.Boxes(TestTables.Cube("room", 0, 0, 0));
        var table = Node.EvalTable([a, b], ("aKey", "EntityIndex"));
        Assert.That(table.Cell("A", 0), Is.EqualTo(7L));
        Assert.That(table.Cell("B", 0), Is.EqualTo("room"));
    }

    [Test]
    public void Empty_B_Gives_An_Empty_Pairs_Table()
    {
        var a = TestTables.Boxes(TestTables.Cube("x", 0, 0, 0));
        var b = TestTables.Boxes();
        var table = Node.EvalTable([a, b]);
        Assert.That(table.Rows.Count, Is.EqualTo(0));
        Assert.That(table.ColumnNames(), Is.EqualTo(new[] { "A", "B", "OverlapVolume" }));
    }

    [Test]
    public void Inverted_Box_Is_An_Error_Naming_The_Row()
    {
        var a = TestTables.Boxes(TestTables.Cube("ok", 0, 0, 0), ("bad", 1, 0, 0, 0, 1, 1));
        var b = TestTables.Boxes(TestTables.Cube("cube", 0, 0, 0));
        Assert.That(() => Node.EvalTable([a, b]),
            Throws.ArgumentException.With.Message.Contains("spatial.intersects: row 1 of input 'a'"));
    }

    [Test]
    public void Missing_Point_Columns_Name_Both_Conventions()
    {
        var a = NodeTestHelpers.Table(("Name", new[] { "x" }), ("Px", new[] { 0.0 }));
        var b = TestTables.Boxes(TestTables.Cube("cube", 0, 0, 0));
        Assert.That(() => Node.EvalTable([a, b]),
            Throws.ArgumentException.With.Message.Contains("MinX..MaxZ").And.Message.Contains("'CenterX'"));
    }

    [Test]
    public void Many_Boxes_Match_Brute_Force()
    {
        var random = new Random(7);
        (string, double, double, double, double, double, double) Cube(int i)
        {
            var x = random.NextDouble() * 50;
            var y = random.NextDouble() * 50;
            var z = random.NextDouble() * 10;
            return ($"b{i}", x, y, z, x + random.NextDouble() * 5, y + random.NextDouble() * 5, z + random.NextDouble() * 3);
        }
        var rows = Enumerable.Range(0, 300).Select(Cube).ToArray();
        var boxes = TestTables.Boxes(rows);
        var expected = new List<(string?, string?)>();
        for (var i = 0; i < rows.Length; i++)
            for (var j = 0; j < rows.Length; j++)
            {
                if (i == j) continue;
                var p = new Box(rows[i].Item2, rows[i].Item3, rows[i].Item4, rows[i].Item5, rows[i].Item6, rows[i].Item7);
                var q = new Box(rows[j].Item2, rows[j].Item3, rows[j].Item4, rows[j].Item5, rows[j].Item6, rows[j].Item7);
                if (p.Intersects(q)) expected.Add((rows[i].Item1, rows[j].Item1));
            }
        Assert.That(Node.EvalTable([boxes, boxes]).KeyPairs(), Is.EqualTo(expected));
    }
}
