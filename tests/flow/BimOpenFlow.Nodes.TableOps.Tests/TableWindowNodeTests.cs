using Ara3D.DataFlowEngine.Abstractions;

namespace BimOpenFlow.Nodes.TableOps.Tests;

[TestFixture]
public class TableWindowNodeTests
{
    private static TableValue Sales()
        => NodeTestHelpers.Table(
            ("region", typeof(string), ["east", "west", "east", "west"]),
            ("amount", typeof(long), [10L, 20L, 30L, 40L]));

    [Test]
    public void RowNumber_Partitions_And_Preserves_Input_Row_Order()
    {
        var table = new TableWindowNode().EvalTable([Sales()],
            ("function", "rowNumber"), ("partitionBy", "region"), ("name", "rn"));
        Assert.That(table.ColumnNames(), Is.EqualTo(new[] { "region", "amount", "rn" }));
        Assert.That(table.ColumnCells("amount"), Is.EqualTo(new[] { 10L, 20L, 30L, 40L }));
        Assert.That(table.ColumnCells("rn"), Is.EqualTo(new[] { 1L, 1L, 2L, 2L }));
    }

    [Test]
    public void Rank_Uses_OrderBy_With_Ties()
    {
        var input = NodeTestHelpers.Table(("v", typeof(long), [10L, 20L, 10L]));
        var table = new TableWindowNode().EvalTable([input],
            ("function", "rank"), ("orderBy", "v"), ("name", "r"));
        Assert.That(table.ColumnCells("r"), Is.EqualTo(new[] { 1L, 3L, 1L }));
    }

    [Test]
    public void Lag_Shifts_By_Offset_Within_Partition()
    {
        var table = new TableWindowNode().EvalTable([Sales()],
            ("function", "lag"), ("column", "amount"), ("partitionBy", "region"), ("name", "prev"));
        Assert.That(table.Cell("prev", 0), Is.Null.Or.EqualTo(DBNull.Value));
        Assert.That(table.Cell("prev", 2), Is.EqualTo(10L));
        Assert.That(table.Cell("prev", 3), Is.EqualTo(20L));
    }

    [Test]
    public void CumSum_Runs_In_Input_Order()
    {
        var table = new TableWindowNode().EvalTable([Sales()],
            ("function", "cumSum"), ("column", "amount"), ("name", "running"));
        Assert.That(table.ColumnCells("running"), Is.EqualTo(new[] { 10L, 30L, 60L, 100L }));
    }

    [Test]
    public void MovingAvg_Uses_WindowSize_Rows()
    {
        var input = NodeTestHelpers.Table(("v", typeof(double), [1.0, 2.0, 3.0, 4.0]));
        var table = new TableWindowNode().EvalTable([input],
            ("function", "movingAvg"), ("column", "v"), ("windowSize", "2"), ("name", "avg"));
        Assert.That(table.ColumnCells("avg"), Is.EqualTo(new[] { 1.0, 1.5, 2.5, 3.5 }));
    }

    [Test]
    public void PercentOfTotal_Divides_By_The_Partition_Sum()
    {
        var table = new TableWindowNode().EvalTable([Sales()],
            ("function", "percentOfTotal"), ("column", "amount"), ("partitionBy", "region"),
            ("name", "share"));
        Assert.That((double)table.Cell("share", 0)!, Is.EqualTo(0.25).Within(1e-9));
        Assert.That((double)table.Cell("share", 3)!, Is.EqualTo(40.0 / 60).Within(1e-9));
    }

    [Test]
    public void Existing_Name_Is_An_Error()
    {
        Assert.That(
            () => new TableWindowNode().EvalTable([Sales()],
                ("function", "rowNumber"), ("name", "amount")),
            Throws.ArgumentException.With.Message.StartsWith("table.window:"));
    }

    [Test]
    public void Missing_Column_For_Lag_Is_An_Error()
    {
        Assert.That(
            () => new TableWindowNode().EvalTable([Sales()],
                ("function", "lag"), ("name", "prev")),
            Throws.ArgumentException.With.Message.StartsWith("table.window:"));
    }
}
