namespace BimOpenFlow.Nodes.Viz.Tests;

[TestFixture]
public class ChartLineNodeTests
{
    [Test]
    public void Projects_X_Then_Ys_Sorted_Ascending_By_Numeric_X()
    {
        var table = new ChartLineNode().EvalTable([VizTestTables.Sample()],
            ("xColumn", "count"), ("yColumns", "cost"));
        Assert.That(table.ColumnNames(), Is.EqualTo(new[] { "count", "cost" }));
        Assert.That(table.ColumnCells("count"), Is.EqualTo(new[] { 1L, 2L, 3L }));
        Assert.That(table.ColumnCells("cost"), Is.EqualTo(new[] { 2.5, 1.5, 0.5 }));
    }

    [Test]
    public void Sorts_Text_X_Ordinally()
    {
        var table = new ChartLineNode().EvalTable([VizTestTables.Sample()],
            ("xColumn", "name"), ("yColumns", "count"));
        Assert.That(table.ColumnCells("name"), Is.EqualTo(new[] { "a", "b", "c" }));
        Assert.That(table.ColumnCells("count"), Is.EqualTo(new[] { 3L, 2L, 1L }));
    }

    [Test]
    public void Keeps_Y_Columns_In_Param_Order_And_Trims()
    {
        var table = new ChartLineNode().EvalTable([VizTestTables.Sample()],
            ("xColumn", "name"), ("yColumns", " cost , count "));
        Assert.That(table.ColumnNames(), Is.EqualTo(new[] { "name", "cost", "count" }));
    }

    [Test]
    public void Empty_X_Keeps_Input_Order_And_Projects_Only_Ys()
    {
        var table = new ChartLineNode().EvalTable([VizTestTables.Sample()],
            ("yColumns", "count"));
        Assert.That(table.ColumnNames(), Is.EqualTo(new[] { "count" }));
        Assert.That(table.ColumnCells("count"), Is.EqualTo(new[] { 2L, 3L, 1L }));
    }

    [Test]
    public void Absent_X_Warns_And_Keeps_Input_Order()
    {
        var (table, warnings) = new ChartLineNode().EvalWithWarnings([VizTestTables.Sample()],
            ("xColumn", "bogus"), ("yColumns", "count"));
        Assert.That(table.ColumnNames(), Is.EqualTo(new[] { "count" }));
        Assert.That(table.ColumnCells("count"), Is.EqualTo(new[] { 2L, 3L, 1L }));
        Assert.That(warnings, Is.EqualTo(new[] { "chart.line: no column named 'bogus'" }));
    }

    [Test]
    public void Unknown_Y_Column_Warns_And_Is_Skipped()
    {
        var (table, warnings) = new ChartLineNode().EvalWithWarnings([VizTestTables.Sample()],
            ("xColumn", "count"), ("yColumns", "bogus, cost"));
        Assert.That(table.ColumnNames(), Is.EqualTo(new[] { "count", "cost" }));
        Assert.That(warnings, Is.EqualTo(new[] { "chart.line: no column named 'bogus'" }));
    }

    [Test]
    public void Empty_Y_Columns_Default_To_All_Numeric_Except_X()
    {
        var table = new ChartLineNode().EvalTable([VizTestTables.Sample()],
            ("xColumn", "count"));
        Assert.That(table.ColumnNames(), Is.EqualTo(new[] { "count", "cost" }));
    }

    [Test]
    public void Sorting_Does_Not_Mutate_The_Input_Table()
    {
        var input = VizTestTables.Sample();
        new ChartLineNode().EvalTable([input], ("xColumn", "count"));
        Assert.That(input.Table.ColumnCells("count"), Is.EqualTo(new[] { 2L, 3L, 1L }));
    }
}
