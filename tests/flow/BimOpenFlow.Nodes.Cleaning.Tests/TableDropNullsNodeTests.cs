using Ara3D.DataFlowEngine.Abstractions;
using Ara3D.DataTable;

namespace BimOpenFlow.Nodes.Cleaning.Tests;

[TestFixture]
public class TableDropNullsNodeTests
{
    private static TableValue Rows()
    {
        var builder = new DataTableBuilder("t");
        builder.AddColumn(new object?[] { "a", null, "c", null }, "x", typeof(string));
        builder.AddColumn(new object?[] { 1L, 2L, null, null }, "y", typeof(long));
        return new TableValue(builder.Build());
    }

    [Test]
    public void Any_Drops_Rows_With_Any_Listed_Null()
    {
        var context = new FakeEvalContext();
        var node = new TableDropNullsNode();
        var table = ((TableValue)node.Eval(context, [Rows()],
            NodeTestHelpers.Params(("columns", "x,y")))[0]).Table;
        Assert.That(table.Rows.Count, Is.EqualTo(1));
        Assert.That(table.Cell("x", 0), Is.EqualTo("a"));
        Assert.That(context.Warnings, Has.One.Contains("dropped 3 row(s)"));
    }

    [Test]
    public void All_Drops_Rows_Where_All_Listed_Are_Null()
    {
        var table = new TableDropNullsNode().EvalTable([Rows()],
            ("columns", "x,y"), ("mode", "all"));
        Assert.That(table.Rows.Count, Is.EqualTo(3));
        Assert.That(table.Cell("y", 1), Is.EqualTo(2L));
        Assert.That(table.Cell("x", 2), Is.EqualTo("c"));
    }

    [Test]
    public void Empty_Columns_Means_All_Columns()
    {
        var table = new TableDropNullsNode().EvalTable([Rows()]);
        Assert.That(table.Rows.Count, Is.EqualTo(1));
    }

    [Test]
    public void Single_Column_Keeps_Order_Of_Survivors()
    {
        var table = new TableDropNullsNode().EvalTable([Rows()], ("columns", "x"));
        Assert.That(table.Rows.Count, Is.EqualTo(2));
        Assert.That(table.Cell("x", 0), Is.EqualTo("a"));
        Assert.That(table.Cell("x", 1), Is.EqualTo("c"));
    }

    [Test]
    public void No_Drops_Emits_No_Warning()
    {
        var context = new FakeEvalContext();
        var table = ((TableValue)new TableDropNullsNode().Eval(context, [Rows()],
            NodeTestHelpers.Params(("columns", "x"), ("mode", "all")))[0]).Table;
        Assert.That(table.Rows.Count, Is.EqualTo(2));
        var noDrop = ((TableValue)new TableDropNullsNode().Eval(context, [new TableValue(table)],
            NodeTestHelpers.Params(("columns", "x")))[0]).Table;
        Assert.That(noDrop.Rows.Count, Is.EqualTo(2));
        Assert.That(context.Warnings.Count, Is.EqualTo(1), "only the first eval dropped rows");
    }

    [Test]
    public void Unknown_Column_Is_An_Error()
    {
        Assert.That(
            () => new TableDropNullsNode().EvalTable([Rows()], ("columns", "nope")),
            Throws.ArgumentException.With.Message.StartsWith("table.dropNulls:"));
    }

    [Test]
    public void Bad_Mode_Is_An_Error()
    {
        Assert.That(
            () => new TableDropNullsNode().EvalTable([Rows()], ("mode", "some")),
            Throws.ArgumentException.With.Message.StartsWith("table.dropNulls:"));
    }
}
