using Ara3D.DataFlowEngine.Abstractions;

namespace BimOpenFlow.Nodes.TableOps.Tests;

[TestFixture]
public class TableRenameNodeTests
{
    private static TableValue Input()
        => NodeTestHelpers.Table(
            ("a", typeof(long), [1L]), ("b", typeof(string), ["x"]), ("c", typeof(double), [1.5]));

    [Test]
    public void Renames_Columns_Keeping_Order_And_Data()
    {
        var table = new TableRenameNode().EvalTable([Input()], ("renames", "a=id, c=score"));
        Assert.That(table.ColumnNames(), Is.EqualTo(new[] { "id", "b", "score" }));
        Assert.That(table.Cell("id", 0), Is.EqualTo(1L));
        Assert.That(table.Cell("score", 0), Is.EqualTo(1.5));
    }

    [Test]
    public void Unknown_Old_Name_Warns_And_Skips()
    {
        var (table, warnings) = new TableRenameNode()
            .EvalWithWarnings([Input()], ("renames", "missing=z, a=id"));
        Assert.That(table.ColumnNames(), Is.EqualTo(new[] { "id", "b", "c" }));
        Assert.That(warnings, Has.One.Contains("missing"));
    }

    [Test]
    public void New_Name_Colliding_With_Remaining_Column_Is_An_Error()
    {
        Assert.That(
            () => new TableRenameNode().EvalTable([Input()], ("renames", "a=b")),
            Throws.ArgumentException.With.Message.StartsWith("table.rename:"));
    }

    [Test]
    public void Malformed_Pair_Is_An_Error()
    {
        Assert.That(
            () => new TableRenameNode().EvalTable([Input()], ("renames", "a")),
            Throws.ArgumentException.With.Message.StartsWith("table.rename:"));
    }
}
