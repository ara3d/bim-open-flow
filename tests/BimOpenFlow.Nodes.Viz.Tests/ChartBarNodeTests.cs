namespace BimOpenFlow.Nodes.Viz.Tests;

[TestFixture]
public class ChartBarNodeTests
{
    [Test]
    public void Projects_Label_Then_Values_In_Param_Order()
    {
        var table = new ChartBarNode().EvalTable([VizTestTables.Sample()],
            ("labelColumn", "name"), ("valueColumns", "cost, count"));
        Assert.That(table.ColumnNames(), Is.EqualTo(new[] { "name", "cost", "count" }));
        Assert.That(table.ColumnCells("name"), Is.EqualTo(new[] { "b", "a", "c" }));
    }

    [Test]
    public void Trims_Comma_Separated_Value_Columns()
    {
        var table = new ChartBarNode().EvalTable([VizTestTables.Sample()],
            ("labelColumn", "name"), ("valueColumns", " cost ,  count "));
        Assert.That(table.ColumnNames(), Is.EqualTo(new[] { "name", "cost", "count" }));
    }

    [Test]
    public void Sorts_Ascending_By_First_Value_Column_Numerically()
    {
        var table = new ChartBarNode().EvalTable([VizTestTables.Sample()],
            ("labelColumn", "name"), ("valueColumns", "count"), ("sort", "asc"));
        Assert.That(table.ColumnCells("count"), Is.EqualTo(new[] { 1L, 2L, 3L }));
        Assert.That(table.ColumnCells("name"), Is.EqualTo(new[] { "c", "b", "a" }));
    }

    [Test]
    public void Sorts_Descending_By_First_Value_Column()
    {
        var table = new ChartBarNode().EvalTable([VizTestTables.Sample()],
            ("labelColumn", "name"), ("valueColumns", "cost, count"), ("sort", "desc"));
        Assert.That(table.ColumnCells("cost"), Is.EqualTo(new[] { 2.5, 1.5, 0.5 }));
        Assert.That(table.ColumnCells("name"), Is.EqualTo(new[] { "c", "b", "a" }));
    }

    [Test]
    public void Sorts_Text_Value_Column_Ordinally()
    {
        var input = NodeTestHelpers.Table(
            ("label", typeof(string), ["x", "y", "z"]),
            ("grade", typeof(string), ["b", "a", "c"]));
        var table = new ChartBarNode().EvalTable([input],
            ("labelColumn", "label"), ("valueColumns", "grade"), ("sort", "asc"));
        Assert.That(table.ColumnCells("grade"), Is.EqualTo(new[] { "a", "b", "c" }));
        Assert.That(table.ColumnCells("label"), Is.EqualTo(new[] { "y", "x", "z" }));
    }

    [Test]
    public void Unknown_Value_Column_Warns_And_Is_Skipped()
    {
        var (table, warnings) = new ChartBarNode().EvalWithWarnings([VizTestTables.Sample()],
            ("labelColumn", "name"), ("valueColumns", "count, bogus"));
        Assert.That(table.ColumnNames(), Is.EqualTo(new[] { "name", "count" }));
        Assert.That(warnings, Is.EqualTo(new[] { "chart.bar: no column named 'bogus'" }));
    }

    [Test]
    public void Empty_Label_Falls_Back_To_First_Text_Column_Without_Warning()
    {
        var (table, warnings) = new ChartBarNode().EvalWithWarnings([VizTestTables.Sample()],
            ("valueColumns", "count"));
        Assert.That(table.ColumnNames(), Is.EqualTo(new[] { "name", "count" }));
        Assert.That(warnings, Is.Empty);
    }

    [Test]
    public void Absent_Label_Warns_And_Falls_Back_To_First_Text_Column()
    {
        var (table, warnings) = new ChartBarNode().EvalWithWarnings([VizTestTables.Sample()],
            ("labelColumn", "bogus"), ("valueColumns", "count"));
        Assert.That(table.ColumnNames(), Is.EqualTo(new[] { "name", "count" }));
        Assert.That(warnings, Is.EqualTo(new[] { "chart.bar: no column named 'bogus'" }));
    }

    [Test]
    public void Empty_Value_Columns_Default_To_All_Numeric_Except_Label()
    {
        var table = new ChartBarNode().EvalTable([VizTestTables.Sample()],
            ("labelColumn", "name"));
        Assert.That(table.ColumnNames(), Is.EqualTo(new[] { "name", "count", "cost" }));
    }

    [Test]
    public void Sorting_Does_Not_Mutate_The_Input_Table()
    {
        var input = VizTestTables.Sample();
        new ChartBarNode().EvalTable([input],
            ("labelColumn", "name"), ("valueColumns", "count"), ("sort", "asc"));
        Assert.That(input.Table.ColumnCells("count"), Is.EqualTo(new[] { 2L, 3L, 1L }));
        Assert.That(input.Table.ColumnNames(), Is.EqualTo(new[] { "name", "count", "cost" }));
    }
}
