using Ara3D.DataFlowEngine.Abstractions;

namespace BimOpenFlow.Nodes.TableOps.Tests;

[TestFixture]
public class TablePivotNodeTests
{
    private static TableValue Long()
        => NodeTestHelpers.Table(
            ("level", typeof(string), ["L1", "L1", "L2", "L2", "L1"]),
            ("month", typeof(string), ["Feb", "Jan", "Jan", "Feb", "Jan"]),
            ("cost", typeof(long), [10L, 20L, 30L, 40L, 5L]));

    [Test]
    public void Pivots_With_Sum_And_Sorted_Value_Columns()
    {
        var table = new TablePivotNode().EvalTable([Long()],
            ("groupBy", "level"), ("nameColumn", "month"), ("valueColumn", "cost"),
            ("aggregate", "sum"));
        Assert.That(table.ColumnNames(), Is.EqualTo(new[] { "level", "Feb", "Jan" }));
        Assert.That(table.ColumnCells("level"), Is.EqualTo(new[] { "L1", "L2" }));
        Assert.That(table.ColumnCells("Jan"), Is.EqualTo(new[] { 25L, 30L }));
        Assert.That(table.ColumnCells("Feb"), Is.EqualTo(new[] { 10L, 40L }));
    }

    [Test]
    public void First_Aggregate_Takes_The_First_Value_In_Input_Order()
    {
        var table = new TablePivotNode().EvalTable([Long()],
            ("groupBy", "level"), ("nameColumn", "month"), ("valueColumn", "cost"));
        Assert.That(table.ColumnCells("Jan"), Is.EqualTo(new[] { 20L, 30L }));
    }

    [Test]
    public void Count_Aggregate_Counts_Rows_Per_Cell()
    {
        var table = new TablePivotNode().EvalTable([Long()],
            ("groupBy", "level"), ("nameColumn", "month"), ("valueColumn", "cost"),
            ("aggregate", "count"));
        Assert.That(table.ColumnCells("Jan"), Is.EqualTo(new[] { 2L, 1L }));
    }

    [Test]
    public void Missing_GroupBy_Is_An_Error()
    {
        Assert.That(
            () => new TablePivotNode().EvalTable([Long()],
                ("nameColumn", "month"), ("valueColumn", "cost")),
            Throws.ArgumentException.With.Message.StartsWith("table.pivot:"));
    }
}
