using Ara3D.DataFlowEngine.Abstractions;
using Ara3D.DataTable;

namespace BimOpenFlow.Nodes.Cleaning.Tests;

[TestFixture]
public class TableDedupeNodeTests
{
    private static TableValue Revisions()
    {
        var builder = new DataTableBuilder("t");
        builder.AddColumn(new object?[] { "d1", "d2", "d1", "d3", "d2" }, "id", typeof(string));
        builder.AddColumn(new object?[] { 1L, 5L, 3L, 1L, 2L }, "rev", typeof(long));
        return new TableValue(builder.Build());
    }

    [Test]
    public void Keep_First_Uses_Input_Row_Order()
    {
        var context = new FakeEvalContext();
        var table = ((TableValue)new TableDedupeNode().Eval(context, [Revisions()],
            NodeTestHelpers.Params(("keys", "id")))[0]).Table;
        Assert.That(table.Rows.Count, Is.EqualTo(3));
        Assert.That(table.Cell("id", 0), Is.EqualTo("d1"));
        Assert.That(table.Cell("rev", 0), Is.EqualTo(1L));
        Assert.That(table.Cell("rev", 1), Is.EqualTo(5L));
        Assert.That(context.Warnings, Has.One.Contains("2 duplicate"));
    }

    [Test]
    public void Keep_Last_Uses_Input_Row_Order()
    {
        var table = new TableDedupeNode().EvalTable([Revisions()],
            ("keys", "id"), ("keep", "last"));
        Assert.That(table.Rows.Count, Is.EqualTo(3));
        Assert.That(table.Cell("rev", 0), Is.EqualTo(3L), "later d1 row wins");
        Assert.That(table.Cell("rev", 2), Is.EqualTo(2L), "later d2 row wins");
    }

    [Test]
    public void Keep_Last_By_OrderBy_Column()
    {
        var table = new TableDedupeNode().EvalTable([Revisions()],
            ("keys", "id"), ("keep", "last"), ("orderBy", "rev"));
        Assert.That(table.Rows.Count, Is.EqualTo(3));
        Assert.That(table.Cell("id", 0), Is.EqualTo("d2"), "kept d2 row (rev 5) precedes kept d1 row (rev 3) in input order");
        Assert.That(table.Cell("rev", 0), Is.EqualTo(5L), "highest rev for d2");
        Assert.That(table.Cell("rev", 1), Is.EqualTo(3L), "highest rev for d1");
    }

    [Test]
    public void Keep_First_By_OrderBy_Desc_Keeps_Highest()
    {
        var table = new TableDedupeNode().EvalTable([Revisions()],
            ("keys", "id"), ("orderBy", "rev desc"));
        Assert.That(table.Cell("rev", 0), Is.EqualTo(5L), "kept d2 row comes first in input order");
        Assert.That(table.Cell("rev", 1), Is.EqualTo(3L));
    }

    [Test]
    public void Output_Preserves_Input_Order_Of_Kept_Rows()
    {
        var table = new TableDedupeNode().EvalTable([Revisions()],
            ("keys", "id"), ("keep", "last"), ("orderBy", "rev"));
        Assert.That(table.Cell("id", 0), Is.EqualTo("d2"), "kept d2 row is input row 1");
        Assert.That(table.Cell("id", 1), Is.EqualTo("d1"), "kept d1 row is input row 2");
        Assert.That(table.Cell("id", 2), Is.EqualTo("d3"));
    }

    [Test]
    public void No_Duplicates_Emits_No_Warning()
    {
        var context = new FakeEvalContext();
        var builder = new DataTableBuilder("t");
        builder.AddColumn(new object?[] { "a", "b" }, "id", typeof(string));
        var table = ((TableValue)new TableDedupeNode().Eval(context, [new TableValue(builder.Build())],
            NodeTestHelpers.Params(("keys", "id")))[0]).Table;
        Assert.That(table.Rows.Count, Is.EqualTo(2));
        Assert.That(context.Warnings, Is.Empty);
    }

    [Test]
    public void Missing_Keys_Is_An_Error()
    {
        Assert.That(
            () => new TableDedupeNode().EvalTable([Revisions()]),
            Throws.ArgumentException.With.Message.StartsWith("table.dedupe:"));
    }

    [Test]
    public void Bad_OrderBy_Term_Is_An_Error()
    {
        Assert.That(
            () => new TableDedupeNode().EvalTable([Revisions()],
                ("keys", "id"), ("orderBy", "rev sideways")),
            Throws.ArgumentException.With.Message.StartsWith("table.dedupe:"));
    }
}
