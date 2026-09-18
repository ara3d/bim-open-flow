namespace BimOpenFlow.Nodes.Spatial.Tests;

[TestFixture]
public class NearestNodeTests
{
    private static readonly NearestNode Node = new();

    private static readonly Ara3D.DataFlowEngine.Abstractions.TableValue Rooms = TestTables.Points(("r1", 0, 0, 0), ("r2", 10, 0, 0));

    private static readonly Ara3D.DataFlowEngine.Abstractions.TableValue Doors = TestTables.Points(
        ("d1", 1, 0, 0), ("d2", 3, 0, 0), ("d3", 9, 0, 0), ("d4", 12, 0, 0));

    [Test]
    public void K_One_Gives_The_Closest_With_Rank_One()
    {
        var table = Node.EvalTable([Rooms, Doors]);
        Assert.That(table.ColumnNames(), Is.EqualTo(new[] { "A", "B", "Distance", "Rank" }));
        Assert.That(table.KeyPairs(), Is.EqualTo(new[] { ("r1", "d1"), ("r2", "d3") }));
        Assert.That(table.ColumnCells("Distance"), Is.EqualTo(new object[] { 1.0, 1.0 }));
        Assert.That(table.ColumnCells("Rank"), Is.EqualTo(new object[] { 1L, 1L }));
    }

    [Test]
    public void K_Three_Ranks_Nearest_First()
    {
        var table = Node.EvalTable([Rooms, Doors], ("k", "3"));
        Assert.That(table.KeyPairs(), Is.EqualTo(new[]
        {
            ("r1", "d1"), ("r1", "d2"), ("r1", "d3"),
            ("r2", "d3"), ("r2", "d4"), ("r2", "d2"),
        }));
        Assert.That(table.ColumnCells("Rank"), Is.EqualTo(new object[] { 1L, 2L, 3L, 1L, 2L, 3L }));
    }

    [Test]
    public void K_Larger_Than_B_Returns_All_Of_B()
    {
        var table = Node.EvalTable([Rooms, Doors], ("k", "10"));
        Assert.That(table.Rows.Count, Is.EqualTo(8));
    }

    [Test]
    public void Ties_Keep_B_Row_Order()
    {
        var b = TestTables.Points(("east", 1, 0, 0), ("west", -1, 0, 0));
        var table = Node.EvalTable([TestTables.Points(("o", 0, 0, 0)), b], ("k", "2"));
        Assert.That(table.KeyPairs(), Is.EqualTo(new[] { ("o", "east"), ("o", "west") }));
    }

    [Test]
    public void Box_Measure_Is_Zero_For_Intersecting_Boxes()
    {
        var a = TestTables.Boxes(TestTables.Cube("duct", 0, 0, 0, 3));
        var b = TestTables.Boxes(TestTables.Cube("beam", 2, 2, 2), TestTables.Cube("post", 5, 0, 0));
        var table = Node.EvalTable([a, b]);
        Assert.That(table.KeyPairs(), Is.EqualTo(new[] { ("duct", "beam") }));
        Assert.That(table.Cell("Distance", 0), Is.EqualTo(0.0));
    }

    [Test]
    public void Self_Join_Skips_Itself()
    {
        var table = Node.EvalTable([Doors, Doors]);
        Assert.That(table.KeyPairs(), Is.EqualTo(new[] { ("d1", "d2"), ("d2", "d1"), ("d3", "d4"), ("d4", "d3") }));
    }

    [Test]
    public void Empty_B_Or_Missing_Coordinates_Produce_No_Rows()
    {
        Assert.That(Node.EvalTable([Rooms, TestTables.Points()]).Rows.Count, Is.EqualTo(0));
        var gap = NodeTestHelpers.Table(("Name", new[] { "gap" }),
            ("CenterX", new object?[] { null }), ("CenterY", new object?[] { 0.0 }), ("CenterZ", new object?[] { 0.0 }));
        Assert.That(Node.EvalTable([gap, Doors]).Rows.Count, Is.EqualTo(0));
    }

    [Test]
    public void K_Below_One_Is_An_Error()
        => Assert.That(() => Node.EvalTable([Rooms, Doors], ("k", "0")),
            Throws.ArgumentException.With.Message.StartsWith("spatial.nearest: "));

    [Test]
    public void Radius_Search_Matches_Brute_Force_On_Many_Points()
    {
        var random = new Random(11);
        var a = Enumerable.Range(0, 40).Select(i => ($"a{i}", random.NextDouble() * 100, random.NextDouble() * 100, random.NextDouble() * 10)).ToArray();
        var b = Enumerable.Range(0, 500).Select(i => ($"b{i}", random.NextDouble() * 100, random.NextDouble() * 100, random.NextDouble() * 10)).ToArray();
        var table = Node.EvalTable([TestTables.Points(a), TestTables.Points(b)], ("k", "3"));
        var expected = a.SelectMany(p => b
                .Select((q, j) => (Key: q.Item1, Row: j, D: Math.Sqrt((p.Item2 - q.Item2) * (p.Item2 - q.Item2) + (p.Item3 - q.Item3) * (p.Item3 - q.Item3) + (p.Item4 - q.Item4) * (p.Item4 - q.Item4))))
                .OrderBy(q => q.D).ThenBy(q => q.Row).Take(3)
                .Select(q => ((string?)p.Item1, (string?)q.Key)))
            .ToList();
        Assert.That(table.KeyPairs(), Is.EqualTo(expected));
    }
}
