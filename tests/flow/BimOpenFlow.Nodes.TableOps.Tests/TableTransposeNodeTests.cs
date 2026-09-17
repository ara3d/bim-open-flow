using Ara3D.DataFlowEngine.Abstractions;

namespace BimOpenFlow.Nodes.TableOps.Tests;

[TestFixture]
public class TableTransposeNodeTests
{
    private static TableValue Summary()
        => NodeTestHelpers.Table(
            ("metric", typeof(string), ["count", "total"]),
            ("walls", typeof(long), [10L, 100L]),
            ("doors", typeof(long), [5L, 50L]));

    [Test]
    public void First_Column_Supplies_Headers_By_Default()
    {
        var table = new TableTransposeNode().EvalTable([Summary()]);
        Assert.That(table.ColumnNames(), Is.EqualTo(new[] { "metric", "count", "total" }));
        Assert.That(table.ColumnCells("metric"), Is.EqualTo(new[] { "walls", "doors" }));
        Assert.That(table.ColumnCells("count"), Is.EqualTo(new[] { "10", "5" }));
        Assert.That(table.ColumnCells("total"), Is.EqualTo(new[] { "100", "50" }));
    }

    [Test]
    public void Named_Header_Column_Is_Used()
    {
        var input = NodeTestHelpers.Table(
            ("v", typeof(long), [1L, 2L]),
            ("h", typeof(string), ["a", "b"]));
        var table = new TableTransposeNode().EvalTable([input], ("headerColumn", "h"));
        Assert.That(table.ColumnNames(), Is.EqualTo(new[] { "h", "a", "b" }));
        Assert.That(table.ColumnCells("h"), Is.EqualTo(new[] { "v" }));
        Assert.That(table.Cell("a", 0), Is.EqualTo("1"));
    }

    [Test]
    public void Duplicate_Header_Value_Is_An_Error()
    {
        var input = NodeTestHelpers.Table(
            ("h", typeof(string), ["a", "a"]), ("v", typeof(long), [1L, 2L]));
        Assert.That(
            () => new TableTransposeNode().EvalTable([input]),
            Throws.ArgumentException.With.Message.StartsWith("table.transpose:"));
    }

    [Test]
    public void More_Than_1000_Rows_Is_An_Error()
    {
        var input = NodeTestHelpers.Table(
            ("h", typeof(long), Enumerable.Range(0, 1001).Select(i => (object?)(long)i).ToArray()));
        Assert.That(
            () => new TableTransposeNode().EvalTable([input]),
            Throws.ArgumentException.With.Message.StartsWith("table.transpose:"));
    }
}
