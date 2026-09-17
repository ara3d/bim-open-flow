using Ara3D.DataFlowEngine.Abstractions;

namespace BimOpenFlow.Nodes.TableOps.Tests;

[TestFixture]
public class TableDistinctNodeTests
{
    private static TableValue Dupes()
        => NodeTestHelpers.Table(
            ("k", typeof(string), ["a", "b", "a", "b", "c"]),
            ("v", typeof(long), [1L, 2L, 3L, 4L, 5L]));

    [Test]
    public void Whole_Row_Distinct_Keeps_First_Occurrence_Order()
    {
        var input = NodeTestHelpers.Table(
            ("k", typeof(string), ["b", "a", "b", "a"]),
            ("v", typeof(long), [1L, 2L, 1L, 2L]));
        var table = new TableDistinctNode().EvalTable([input]);
        Assert.That(table.Rows.Count, Is.EqualTo(2));
        Assert.That(table.ColumnCells("k"), Is.EqualTo(new[] { "b", "a" }));
        Assert.That(table.ColumnCells("v"), Is.EqualTo(new[] { 1L, 2L }));
    }

    [Test]
    public void Keyed_Distinct_Keeps_First_Row_Per_Key_With_All_Columns()
    {
        var table = new TableDistinctNode().EvalTable([Dupes()], ("columns", "k"));
        Assert.That(table.ColumnNames(), Is.EqualTo(new[] { "k", "v" }));
        Assert.That(table.ColumnCells("k"), Is.EqualTo(new[] { "a", "b", "c" }));
        Assert.That(table.ColumnCells("v"), Is.EqualTo(new[] { 1L, 2L, 5L }));
    }

    [Test]
    public void Unknown_Key_Column_Is_An_Error()
    {
        Assert.That(
            () => new TableDistinctNode().EvalTable([Dupes()], ("columns", "missing")),
            Throws.ArgumentException.With.Message.StartsWith("table.distinct:"));
    }
}
