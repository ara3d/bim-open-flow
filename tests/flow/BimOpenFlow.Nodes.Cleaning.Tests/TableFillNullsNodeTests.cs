using Ara3D.DataFlowEngine.Abstractions;
using Ara3D.DataTable;

namespace BimOpenFlow.Nodes.Cleaning.Tests;

[TestFixture]
public class TableFillNullsNodeTests
{
    private static TableValue Rooms()
    {
        var builder = new DataTableBuilder("t");
        builder.AddColumn(new object?[] { "A", "A", "B", "B", "B" }, "zone", typeof(string));
        builder.AddColumn(new object?[] { "Office", null, null, "Lab", null }, "category", typeof(string));
        builder.AddColumn(new object?[] { 1L, null, 3L, null, 5L }, "count", typeof(long));
        return new TableValue(builder.Build());
    }

    [Test]
    public void Constant_Fills_Text_Column()
    {
        var table = new TableFillNullsNode().EvalTable([Rooms()],
            ("columns", "category"), ("value", "Unknown"));
        Assert.That(table.Cell("category", 1), Is.EqualTo("Unknown"));
        Assert.That(table.Cell("category", 3), Is.EqualTo("Lab"));
        Assert.That(table.Cell("count", 1), Is.Null);
    }

    [Test]
    public void Constant_Fills_Integer_Column()
    {
        var table = new TableFillNullsNode().EvalTable([Rooms()],
            ("columns", "count"), ("value", "0"));
        Assert.That(table.Cell("count", 1), Is.EqualTo(0L));
        Assert.That(table.Cell("count", 3), Is.EqualTo(0L));
        Assert.That(table.Cell("count", 4), Is.EqualTo(5L));
        Assert.That(table.Cell("count", 2), Is.EqualTo(3L));
    }

    [Test]
    public void Constant_Value_Not_Castable_Is_An_Error()
    {
        Assert.That(
            () => new TableFillNullsNode().EvalTable([Rooms()],
                ("columns", "count"), ("value", "many")),
            Throws.ArgumentException.With.Message.StartsWith("table.fillNulls:"));
    }

    [Test]
    public void Forward_Fill_Carries_Last_Non_Null_Down()
    {
        var table = new TableFillNullsNode().EvalTable([Rooms()],
            ("columns", "category"), ("strategy", "forward"));
        Assert.That(table.Cell("category", 1), Is.EqualTo("Office"));
        Assert.That(table.Cell("category", 2), Is.EqualTo("Office"));
        Assert.That(table.Cell("category", 4), Is.EqualTo("Lab"));
    }

    [Test]
    public void Forward_Fill_Resets_At_Partition_Boundaries()
    {
        var table = new TableFillNullsNode().EvalTable([Rooms()],
            ("columns", "category"), ("strategy", "forward"), ("partitionBy", "zone"));
        Assert.That(table.Cell("category", 1), Is.EqualTo("Office"));
        Assert.That(table.Cell("category", 2), Is.Null, "first row of zone B has nothing to carry");
        Assert.That(table.Cell("category", 4), Is.EqualTo("Lab"));
    }

    [Test]
    public void Backward_Fill_Carries_Next_Non_Null_Up()
    {
        var table = new TableFillNullsNode().EvalTable([Rooms()],
            ("columns", "category"), ("strategy", "backward"));
        Assert.That(table.Cell("category", 1), Is.EqualTo("Lab"));
        Assert.That(table.Cell("category", 2), Is.EqualTo("Lab"));
        Assert.That(table.Cell("category", 4), Is.Null, "nothing after the last row");
    }

    [Test]
    public void Output_Preserves_Row_Order_And_Columns()
    {
        var table = new TableFillNullsNode().EvalTable([Rooms()],
            ("columns", "category"), ("strategy", "forward"));
        Assert.That(table.ColumnNames(), Is.EqualTo(new[] { "zone", "category", "count" }));
        Assert.That(table.Cell("count", 2), Is.EqualTo(3L));
    }

    [Test]
    public void Missing_Columns_Parameter_Is_An_Error()
    {
        Assert.That(
            () => new TableFillNullsNode().EvalTable([Rooms()], ("value", "x")),
            Throws.ArgumentException.With.Message.StartsWith("table.fillNulls:"));
    }

    [Test]
    public void Unknown_Column_Is_An_Error()
    {
        Assert.That(
            () => new TableFillNullsNode().EvalTable([Rooms()], ("columns", "nope"), ("value", "x")),
            Throws.ArgumentException.With.Message.StartsWith("table.fillNulls:"));
    }
}
