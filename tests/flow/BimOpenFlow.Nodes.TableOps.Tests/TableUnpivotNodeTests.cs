using Ara3D.DataFlowEngine.Abstractions;

namespace BimOpenFlow.Nodes.TableOps.Tests;

[TestFixture]
public class TableUnpivotNodeTests
{
    private static TableValue Wide()
        => NodeTestHelpers.Table(
            ("level", typeof(string), ["L1", "L2"]),
            ("Jan", typeof(long), [10L, 30L]),
            ("Feb", typeof(long), [20L, 40L]));

    [Test]
    public void Unpivots_Everything_Not_In_Keep()
    {
        var table = new TableUnpivotNode().EvalTable([Wide()], ("keep", "level"));
        Assert.That(table.ColumnNames(), Is.EqualTo(new[] { "level", "name", "value" }));
        Assert.That(table.ColumnCells("level"), Is.EqualTo(new[] { "L1", "L1", "L2", "L2" }));
        Assert.That(table.ColumnCells("name"), Is.EqualTo(new[] { "Jan", "Feb", "Jan", "Feb" }));
        Assert.That(table.ColumnCells("value"), Is.EqualTo(new[] { 10L, 20L, 30L, 40L }));
    }

    [Test]
    public void Custom_Name_And_Value_Column_Names()
    {
        var table = new TableUnpivotNode().EvalTable([Wide()],
            ("keep", "level"), ("columns", "Jan"), ("nameColumn", "month"), ("valueColumn", "cost"));
        Assert.That(table.ColumnNames(), Is.EqualTo(new[] { "level", "month", "cost" }));
        Assert.That(table.ColumnCells("cost"), Is.EqualTo(new[] { 10L, 30L }));
    }

    [Test]
    public void Mixed_Types_Widen_To_Text_With_A_Warning()
    {
        var input = NodeTestHelpers.Table(
            ("id", typeof(long), [1L]),
            ("a", typeof(long), [10L]),
            ("b", typeof(string), ["x"]));
        var (table, warnings) = new TableUnpivotNode().EvalWithWarnings([input], ("keep", "id"));
        Assert.That(table.ColumnCells("value"), Is.EqualTo(new[] { "10", "x" }));
        Assert.That(warnings, Has.One.Contains("text"));
    }

    [Test]
    public void No_Columns_To_Unpivot_Is_An_Error()
    {
        var input = NodeTestHelpers.Table(("id", typeof(long), [1L]));
        Assert.That(
            () => new TableUnpivotNode().EvalTable([input], ("keep", "id")),
            Throws.ArgumentException.With.Message.StartsWith("table.unpivot:"));
    }
}
