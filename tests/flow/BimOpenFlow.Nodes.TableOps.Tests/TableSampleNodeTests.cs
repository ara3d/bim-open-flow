using Ara3D.DataFlowEngine.Abstractions;

namespace BimOpenFlow.Nodes.TableOps.Tests;

[TestFixture]
public class TableSampleNodeTests
{
    private static TableValue Numbers(int count)
        => NodeTestHelpers.Table(
            ("n", typeof(long), Enumerable.Range(0, count).Select(i => (object?)(long)i).ToArray()));

    [Test]
    public void Rows_Mode_Returns_Requested_Count_In_Input_Order()
    {
        var table = new TableSampleNode().EvalTable([Numbers(100)], ("rows", "10"), ("seed", "7"));
        Assert.That(table.Rows.Count, Is.EqualTo(10));
        var cells = table.ColumnCells("n").Cast<long>().ToList();
        Assert.That(cells, Is.Ordered.Ascending);
    }

    [Test]
    public void Same_Seed_Gives_Same_Sample()
    {
        var first = new TableSampleNode().EvalTable([Numbers(100)], ("rows", "10"), ("seed", "7"));
        var second = new TableSampleNode().EvalTable([Numbers(100)], ("rows", "10"), ("seed", "7"));
        Assert.That(second.ColumnCells("n"), Is.EqualTo(first.ColumnCells("n")));
    }

    [Test]
    public void Rows_Beyond_Table_Size_Returns_Everything()
    {
        var table = new TableSampleNode().EvalTable([Numbers(5)], ("rows", "100"));
        Assert.That(table.Rows.Count, Is.EqualTo(5));
    }

    [Test]
    public void Fraction_One_Returns_All_Rows()
    {
        var table = new TableSampleNode().EvalTable([Numbers(20)],
            ("mode", "fraction"), ("fraction", "1"), ("seed", "3"));
        Assert.That(table.Rows.Count, Is.EqualTo(20));
    }

    [Test]
    public void Fraction_Outside_Zero_One_Is_An_Error()
    {
        Assert.That(
            () => new TableSampleNode().EvalTable([Numbers(5)],
                ("mode", "fraction"), ("fraction", "1.5")),
            Throws.ArgumentException.With.Message.StartsWith("table.sample:"));
    }
}
