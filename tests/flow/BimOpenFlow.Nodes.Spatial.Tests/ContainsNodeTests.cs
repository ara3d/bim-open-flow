namespace BimOpenFlow.Nodes.Spatial.Tests;

[TestFixture]
public class ContainsNodeTests
{
    private static readonly ContainsNode Node = new();

    private static readonly Ara3D.DataFlowEngine.Abstractions.TableValue Rooms = TestTables.Boxes(
        ("floor", 0, 0, 0, 20, 20, 3), ("office", 0, 0, 0, 5, 4, 3), ("closet", 1, 1, 0, 2, 2, 3));

    [Test]
    public void Smallest_Container_Wins_By_Default()
    {
        var points = TestTables.Points(("desk", 3, 3, 1), ("broom", 1.5, 1.5, 1), ("outside", 50, 50, 1));
        var table = Node.EvalTable([points, Rooms]);
        Assert.That(table.ColumnNames(), Is.EqualTo(new[] { "A", "B", "ContainerVolume" }));
        Assert.That(table.KeyPairs(), Is.EqualTo(new[] { ("desk", "office"), ("broom", "closet") }));
        Assert.That(table.Cell("ContainerVolume", 1), Is.EqualTo(3.0));
    }

    [Test]
    public void Smallest_False_Lists_Every_Container_In_Row_Order()
    {
        var points = TestTables.Points(("broom", 1.5, 1.5, 1));
        var table = Node.EvalTable([points, Rooms], ("smallest", "false"));
        Assert.That(table.KeyPairs(), Is.EqualTo(new[] { ("broom", "floor"), ("broom", "office"), ("broom", "closet") }));
    }

    [Test]
    public void A_Box_Must_Be_Fully_Inside()
    {
        var boxes = TestTables.Boxes(("inside", 1, 1, 0, 4, 3, 2), ("straddling", 4, 3, 0, 6, 5, 2));
        var table = Node.EvalTable([boxes, Rooms]);
        Assert.That(table.KeyPairs(), Is.EqualTo(new[] { ("inside", "office"), ("straddling", "floor") }));
    }

    [Test]
    public void IgnoreZ_Judges_In_Plan()
    {
        var points = TestTables.Points(("high", 3, 3, 40));
        Assert.That(Node.EvalTable([points, Rooms]).Rows.Count, Is.EqualTo(0));
        Assert.That(Node.EvalTable([points, Rooms], ("ignoreZ", "true")).KeyPairs(), Is.EqualTo(new[] { ("high", "office") }));
    }

    [Test]
    public void Self_Join_Excludes_Itself_And_Finds_The_Enclosing_Room()
    {
        var table = Node.EvalTable([Rooms, Rooms]);
        Assert.That(table.KeyPairs(), Is.EqualTo(new[] { ("office", "floor"), ("closet", "office") }));
    }

    [Test]
    public void Boundary_Points_Count_As_Contained()
    {
        var points = TestTables.Points(("corner", 5, 4, 3));
        Assert.That(Node.EvalTable([points, Rooms]).KeyPairs(), Is.EqualTo(new[] { ("corner", "office") }));
    }

    [Test]
    public void Boxes_Input_Without_Box_Columns_Is_An_Error()
        => Assert.That(() => Node.EvalTable([TestTables.Points(("p", 0, 0, 0)), TestTables.Points(("q", 0, 0, 0))]),
            Throws.ArgumentException.With.Message.Contains("'boxes' must have the box columns"));
}
