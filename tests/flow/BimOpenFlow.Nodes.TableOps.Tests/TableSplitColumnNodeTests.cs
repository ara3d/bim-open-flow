using Ara3D.DataFlowEngine.Abstractions;

namespace BimOpenFlow.Nodes.TableOps.Tests;

[TestFixture]
public class TableSplitColumnNodeTests
{
    private static TableValue Keys()
        => NodeTestHelpers.Table(("key", typeof(string), ["L1-ZA-Wall", "L2-ZB", "L3"]));

    [Test]
    public void Splits_Into_Named_Columns_Dropping_The_Original()
    {
        var table = new TableSplitColumnNode().EvalTable([Keys()],
            ("column", "key"), ("names", "level, zone"));
        Assert.That(table.ColumnNames(), Is.EqualTo(new[] { "level", "zone" }));
        Assert.That(table.ColumnCells("level"), Is.EqualTo(new[] { "L1", "L2", "L3" }));
        Assert.That(table.Cell("zone", 0), Is.EqualTo("ZA"));
    }

    [Test]
    public void Fewer_Parts_Than_Names_Yields_Nulls()
    {
        var table = new TableSplitColumnNode().EvalTable([Keys()],
            ("column", "key"), ("names", "level, zone, type"));
        Assert.That(table.Cell("type", 0), Is.EqualTo("Wall"));
        Assert.That(table.Cell("type", 1), Is.Null.Or.EqualTo(DBNull.Value));
        Assert.That(table.Cell("zone", 2), Is.Null.Or.EqualTo(DBNull.Value));
    }

    [Test]
    public void Keep_True_Retains_The_Original_Column()
    {
        var table = new TableSplitColumnNode().EvalTable([Keys()],
            ("column", "key"), ("names", "level"), ("keep", "true"));
        Assert.That(table.ColumnNames(), Is.EqualTo(new[] { "key", "level" }));
    }

    [Test]
    public void Custom_Separator_Is_Used()
    {
        var input = NodeTestHelpers.Table(("v", typeof(string), ["a|b"]));
        var table = new TableSplitColumnNode().EvalTable([input],
            ("column", "v"), ("separator", "|"), ("names", "p1, p2"));
        Assert.That(table.Cell("p2", 0), Is.EqualTo("b"));
    }

    [Test]
    public void New_Name_Colliding_With_Existing_Column_Is_An_Error()
    {
        Assert.That(
            () => new TableSplitColumnNode().EvalTable([Keys()],
                ("column", "key"), ("names", "key, zone"), ("keep", "true")),
            Throws.ArgumentException.With.Message.StartsWith("table.splitColumn:"));
    }
}
