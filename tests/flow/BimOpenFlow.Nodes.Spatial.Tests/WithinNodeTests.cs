namespace BimOpenFlow.Nodes.Spatial.Tests;

[TestFixture]
public class WithinNodeTests
{
    private static readonly WithinNode Node = new();

    private static readonly Ara3D.DataFlowEngine.Abstractions.TableValue Rooms = TestTables.Boxes(
        ("near", 0, 0, 0, 4, 4, 3), ("touching", 6, 0, 0, 10, 4, 3), ("far", 20, 0, 0, 24, 4, 3));

    private static readonly Ara3D.DataFlowEngine.Abstractions.TableValue Stair = TestTables.Boxes(("stair", 5, 0, 0, 6, 2, 3));

    [Test]
    public void Surface_Distance_Is_The_Default_Measure()
    {
        var table = Node.EvalTable([Rooms, Stair], ("distance", "3"));
        Assert.That(table.ColumnNames(), Is.EqualTo(new[] { "A", "B", "Distance" }));
        Assert.That(table.KeyPairs(), Is.EqualTo(new[] { ("near", "stair"), ("touching", "stair") }));
        Assert.That(table.ColumnCells("Distance"), Is.EqualTo(new object[] { 1.0, 0.0 }));
    }

    [Test]
    public void Center_Measure_Uses_Box_Centers()
    {
        // Centers: near (2, 2), touching (8, 2), stair (5.5, 1): 3.64 and 2.69 away in plan.
        var table = Node.EvalTable([Rooms, Stair], ("distance", "3"), ("measure", "center"));
        Assert.That(table.KeyPairs(), Is.EqualTo(new[] { ("touching", "stair") }));
        Assert.That((double)table.Cell("Distance", 0)!, Is.EqualTo(Math.Sqrt(2.5 * 2.5 + 1)).Within(1e-12));
    }

    [Test]
    public void Points_Within_A_Radius()
    {
        var points = TestTables.Points(("p1", 0, 0, 0), ("p2", 0, 2, 0), ("p3", 0, 2.5, 0));
        var origin = TestTables.Points(("o", 0, 0, 0));
        var table = Node.EvalTable([points, origin], ("distance", "2"));
        Assert.That(table.KeyPairs(), Is.EqualTo(new[] { ("p1", "o"), ("p2", "o") }));
    }

    [Test]
    public void Self_Join_Excludes_Same_Key()
    {
        var table = Node.EvalTable([Rooms, Rooms], ("distance", "2"));
        Assert.That(table.KeyPairs(), Is.EqualTo(new[] { ("near", "touching"), ("touching", "near") }));
    }

    [Test]
    public void Negative_Or_Missing_Distance_Is_An_Error()
    {
        Assert.That(() => Node.EvalTable([Rooms, Stair], ("distance", "-1")),
            Throws.ArgumentException.With.Message.StartsWith("spatial.within: "));
        Assert.That(() => Node.EvalTable([Rooms, Stair]),
            Throws.ArgumentException.With.Message.StartsWith("spatial.within: "));
    }

    [Test]
    public void Unknown_Measure_Is_An_Error()
        => Assert.That(() => Node.EvalTable([Rooms, Stair], ("distance", "1"), ("measure", "hausdorff")),
            Throws.ArgumentException.With.Message.Contains("box, center"));
}
