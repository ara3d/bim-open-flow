using Ara3D.DataFlowEngine.Abstractions;
using Ara3D.DataTable;

namespace BimOpenFlow.Nodes.TableOps.Tests;

[TestFixture]
public class TableLimitNodeTests
{
    private static TableValue Numbers(int count)
    {
        var builder = new DataTableBuilder("t");
        builder.AddColumn(Enumerable.Range(0, count).Select(i => (object?)(long)i).ToArray(),
            "n", typeof(long));
        return new TableValue(builder.Build());
    }

    [Test]
    public void Limits_And_Offsets_In_Order()
    {
        var table = new TableLimitNode().EvalTable([Numbers(10)], ("count", "3"), ("offset", "4"));
        Assert.That(table.Rows.Count, Is.EqualTo(3));
        Assert.That(table.Cell("n", 0), Is.EqualTo(4L));
        Assert.That(table.Cell("n", 2), Is.EqualTo(6L));
    }

    [Test]
    public void Count_Beyond_End_Returns_Remaining_Rows()
    {
        var table = new TableLimitNode().EvalTable([Numbers(5)], ("count", "100"), ("offset", "3"));
        Assert.That(table.Rows.Count, Is.EqualTo(2));
    }

    [Test]
    public void Missing_Count_Is_An_Error()
    {
        Assert.That(
            () => new TableLimitNode().EvalTable([Numbers(3)]),
            Throws.ArgumentException.With.Message.Contains("table.limit"));
    }

    [Test]
    public void Negative_Offset_Is_An_Error()
    {
        Assert.That(
            () => new TableLimitNode().EvalTable([Numbers(3)], ("count", "1"), ("offset", "-1")),
            Throws.ArgumentException.With.Message.Contains("table.limit"));
    }
}
