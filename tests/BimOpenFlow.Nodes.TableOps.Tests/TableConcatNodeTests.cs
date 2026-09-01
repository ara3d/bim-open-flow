using Ara3D.DataFlowEngine.Abstractions;

namespace BimOpenFlow.Nodes.TableOps.Tests;

[TestFixture]
public class TableConcatNodeTests
{
    [Test]
    public void Strict_Stacks_A_Then_B()
    {
        var a = NodeTestHelpers.Table(
            ("x", typeof(long), [1L, 2L]), ("y", typeof(string), ["a", "b"]));
        var b = NodeTestHelpers.Table(
            ("x", typeof(long), [3L]), ("y", typeof(string), ["c"]));
        var table = new TableConcatNode().EvalTable([a, b]);
        Assert.That(table.ColumnNames(), Is.EqualTo(new[] { "x", "y" }));
        Assert.That(table.ColumnCells("x"), Is.EqualTo(new[] { 1L, 2L, 3L }));
        Assert.That(table.ColumnCells("y"), Is.EqualTo(new[] { "a", "b", "c" }));
    }

    [Test]
    public void Strict_With_Different_Sequences_Errors_Naming_The_Difference()
    {
        var a = NodeTestHelpers.Table(("x", typeof(long), [1L]));
        var b = NodeTestHelpers.Table(("z", typeof(long), [2L]));
        Assert.That(
            () => new TableConcatNode().EvalTable([a, b]),
            Throws.ArgumentException.With.Message.StartsWith("table.concat:")
                .And.Message.Contains("x").And.Message.Contains("z"));
    }

    [Test]
    public void ByName_Matches_Columns_And_Null_Fills_Missing()
    {
        var a = NodeTestHelpers.Table(
            ("x", typeof(long), [1L]), ("y", typeof(string), ["a"]));
        var b = NodeTestHelpers.Table(
            ("y", typeof(string), ["b"]), ("z", typeof(long), [9L]));
        var table = new TableConcatNode().EvalTable([a, b], ("columns", "byName"));
        Assert.That(table.ColumnNames(), Is.EqualTo(new[] { "x", "y", "z" }));
        Assert.That(table.ColumnCells("y"), Is.EqualTo(new[] { "a", "b" }));
        Assert.That(table.Cell("x", 1), Is.Null.Or.EqualTo(DBNull.Value));
        Assert.That(table.Cell("z", 0), Is.Null.Or.EqualTo(DBNull.Value));
        Assert.That(table.Cell("z", 1), Is.EqualTo(9L));
    }
}
