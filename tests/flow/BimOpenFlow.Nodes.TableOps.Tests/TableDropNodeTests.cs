using Ara3D.DataFlowEngine.Abstractions;

namespace BimOpenFlow.Nodes.TableOps.Tests;

[TestFixture]
public class TableDropNodeTests
{
    private static TableValue Input()
        => NodeTestHelpers.Table(
            ("a", typeof(long), [1L]), ("b", typeof(string), ["x"]), ("c", typeof(double), [1.5]));

    [Test]
    public void Drops_Named_Columns_And_Keeps_The_Rest()
    {
        var table = new TableDropNode().EvalTable([Input()], ("columns", "b"));
        Assert.That(table.ColumnNames(), Is.EqualTo(new[] { "a", "c" }));
        Assert.That(table.Cell("a", 0), Is.EqualTo(1L));
    }

    [Test]
    public void Unknown_Name_Warns_And_Is_Ignored()
    {
        var (table, warnings) = new TableDropNode()
            .EvalWithWarnings([Input()], ("columns", "b, missing"));
        Assert.That(table.ColumnNames(), Is.EqualTo(new[] { "a", "c" }));
        Assert.That(warnings, Has.One.Contains("missing"));
    }

    [Test]
    public void Dropping_Every_Column_Is_An_Error()
    {
        Assert.That(
            () => new TableDropNode().EvalTable([Input()], ("columns", "a, b, c")),
            Throws.ArgumentException.With.Message.StartsWith("table.drop:"));
    }
}
