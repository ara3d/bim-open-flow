using Ara3D.DataFlowEngine.Abstractions;

namespace BimOpenFlow.Nodes.TableOps.Tests;

[TestFixture]
public class TableProfileNodeTests
{
    private static TableValue Input()
        => NodeTestHelpers.Table(
            ("n", typeof(long), [1L, 2L, 3L, null]),
            ("s", typeof(string), ["apple", "banana", "apple", "cherry"]));

    [Test]
    public void Fixed_Column_Set_In_Input_Column_Order()
    {
        var table = new TableProfileNode().EvalTable([Input()]);
        Assert.That(table.ColumnNames(), Is.EqualTo(
            new[] { "column", "type", "count", "nullCount", "distinctCount", "min", "max", "mean" }));
        Assert.That(table.ColumnCells("column"), Is.EqualTo(new[] { "n", "s" }));
        Assert.That(table.ColumnCells("type"), Is.EqualTo(new[] { "Integer", "Text" }));
    }

    [Test]
    public void Numeric_Column_Gets_Counts_Min_Max_And_Mean()
    {
        var table = new TableProfileNode().EvalTable([Input()]);
        Assert.That(table.Cell("count", 0), Is.EqualTo(4L));
        Assert.That(table.Cell("nullCount", 0), Is.EqualTo(1L));
        Assert.That(table.Cell("min", 0), Is.EqualTo("1"));
        Assert.That(table.Cell("max", 0), Is.EqualTo("3"));
        Assert.That((double)table.Cell("mean", 0)!, Is.EqualTo(2.0).Within(1e-9));
    }

    [Test]
    public void Text_Column_Gets_Lexical_Min_Max_And_Null_Mean()
    {
        var table = new TableProfileNode().EvalTable([Input()]);
        Assert.That(table.Cell("min", 1), Is.EqualTo("apple"));
        Assert.That(table.Cell("max", 1), Is.EqualTo("cherry"));
        Assert.That(table.Cell("mean", 1), Is.Null.Or.EqualTo(DBNull.Value));
        Assert.That(table.Cell("distinctCount", 1), Is.EqualTo(3L));
    }
}
