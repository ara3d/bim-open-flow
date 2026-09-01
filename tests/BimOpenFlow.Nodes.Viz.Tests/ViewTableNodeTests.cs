namespace BimOpenFlow.Nodes.Viz.Tests;

[TestFixture]
public class ViewTableNodeTests
{
    [Test]
    public void Empty_Columns_Passes_Table_Through_Unchanged()
    {
        var input = VizTestTables.Sample();
        var table = new ViewTableNode().EvalTable([input]);
        Assert.That(table, Is.SameAs(input.Table));
    }

    [Test]
    public void Projects_Named_Columns_In_Order_And_Trims()
    {
        var table = new ViewTableNode().EvalTable([VizTestTables.Sample()],
            ("columns", " cost , name "));
        Assert.That(table.ColumnNames(), Is.EqualTo(new[] { "cost", "name" }));
        Assert.That(table.ColumnCells("cost"), Is.EqualTo(new[] { 1.5, 0.5, 2.5 }));
    }

    [Test]
    public void Unknown_Column_Warns_And_Is_Skipped()
    {
        var (table, warnings) = new ViewTableNode().EvalWithWarnings([VizTestTables.Sample()],
            ("columns", "name, bogus"));
        Assert.That(table.ColumnNames(), Is.EqualTo(new[] { "name" }));
        Assert.That(warnings, Is.EqualTo(new[] { "view.table: no column named 'bogus'" }));
    }

    [Test]
    public void Title_Does_Not_Affect_The_Output_Table()
    {
        var table = new ViewTableNode().EvalTable([VizTestTables.Sample()],
            ("title", "My View"), ("columns", "name"));
        Assert.That(table.ColumnNames(), Is.EqualTo(new[] { "name" }));
        Assert.That(table.ColumnCells("name"), Is.EqualTo(new[] { "b", "a", "c" }));
    }
}
