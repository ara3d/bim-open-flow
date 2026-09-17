using Ara3D.DataFlowEngine.Abstractions;
using Ara3D.DataTable;

namespace BimOpenFlow.Nodes.Tables.Tests;

/// <summary>Join, set-operation, and projection nodes on small in-code tables:
/// happy paths, null-key handling, and warning surfacing.</summary>
[TestFixture]
public sealed class TableCombinatorTests
{
    [Test]
    public void Pack_ExposesAllNodes()
        => Assert.That(TableNodes.All.Select(n => n.Spec.Kind), Is.EquivalentTo(new[]
        {
            "xlsx.read", "xlsx.sheets",
            "sqlite.query", "sqlite.table", "sqlite.tables",
            "table.join", "table.setOp", "table.project",
            "table.inline", "table.range", "table.calendar",
        }));

    [Test]
    public void Join_Left_KeepsAllARows_NullsWhereUnmatched_AndWarns()
    {
        var ctx = new FakeEvalContext();
        var result = new TableJoinNode().EvalTable(ctx,
            [TablesTestHelpers.Orders(), TablesTestHelpers.Customers()], ("aKey", "CustomerId"));
        Assert.That(result.Rows, Has.Count.EqualTo(4));
        Assert.That(result.ColumnNames(), Is.EqualTo(new[] { "Id", "CustomerId", "Name" }));
        Assert.That(result.Cell("Name", 0), Is.EqualTo("Alice"));
        Assert.That(result.Cell("Name", 1), Is.EqualTo("Bob"));
        Assert.That(result.Cell("Name", 2), Is.Null, "null key never matches");
        Assert.That(result.Cell("Name", 3), Is.Null, "unknown key");
        Assert.That(ctx.Warnings, Has.One.Contains("2 of 4 rows unmatched"));
    }

    [Test]
    public void Join_Inner_KeepsOnlyMatches()
    {
        var ctx = new FakeEvalContext();
        var result = new TableJoinNode().EvalTable(ctx,
            [TablesTestHelpers.Orders(), TablesTestHelpers.Customers()],
            ("aKey", "CustomerId"), ("mode", "inner"));
        Assert.That(result.Rows, Has.Count.EqualTo(2));
        Assert.That(result.Cell("Id", 0), Is.EqualTo(1L));
        Assert.That(result.Cell("Id", 1), Is.EqualTo(2L));
        Assert.That(ctx.Warnings, Has.One.Contains("unmatched"));
    }

    [Test]
    public void Join_Semi_KeepsMatchingARows_NoBColumns_AndWarns()
    {
        var ctx = new FakeEvalContext();
        var result = new TableJoinNode().EvalTable(ctx,
            [TablesTestHelpers.Orders(), TablesTestHelpers.Customers()],
            ("aKey", "CustomerId"), ("mode", "semi"));
        Assert.That(result.ColumnNames(), Is.EqualTo(new[] { "Id", "CustomerId" }));
        Assert.That(result.Rows, Has.Count.EqualTo(2));
        Assert.That(result.Cell("Id", 0), Is.EqualTo(1L));
        Assert.That(result.Cell("Id", 1), Is.EqualTo(2L));
        Assert.That(ctx.Warnings, Has.One.Contains("2 of 4 rows unmatched"));
    }

    [Test]
    public void Join_Anti_KeepsNonMatchingARows_NullKeyCountsAsUnmatched()
    {
        var ctx = new FakeEvalContext();
        var result = new TableJoinNode().EvalTable(ctx,
            [TablesTestHelpers.Orders(), TablesTestHelpers.Customers()],
            ("aKey", "CustomerId"), ("mode", "anti"));
        Assert.That(result.ColumnNames(), Is.EqualTo(new[] { "Id", "CustomerId" }));
        Assert.That(result.Rows, Has.Count.EqualTo(2));
        Assert.That(result.Cell("CustomerId", 0), Is.Null);
        Assert.That(result.Cell("CustomerId", 1), Is.EqualTo("C9"));
        Assert.That(ctx.Warnings, Has.One.Contains("2 of 4 rows unmatched"));
    }

    [Test]
    public void Join_Full_AppendsUnmatchedBRows_WithBKeyInAKeyColumn()
    {
        var ctx = new FakeEvalContext();
        var result = new TableJoinNode().EvalTable(ctx,
            [TablesTestHelpers.Orders(), TablesTestHelpers.Customers()],
            ("aKey", "CustomerId"), ("mode", "full"));
        Assert.That(result.ColumnNames(), Is.EqualTo(new[] { "Id", "CustomerId", "Name" }));
        Assert.That(result.Rows, Has.Count.EqualTo(5), "4 a rows plus unmatched customer C3");
        Assert.That(result.Cell("Name", 0), Is.EqualTo("Alice"));
        Assert.That(result.Cell("Id", 4), Is.Null);
        Assert.That(result.Cell("CustomerId", 4), Is.EqualTo("C3"));
        Assert.That(result.Cell("Name", 4), Is.EqualTo("Carol"));
        Assert.That(ctx.Warnings, Has.One.Contains("2 of 4 rows unmatched"));
    }

    [Test]
    public void Join_Full_AllMatched_AppendsNothing()
    {
        var b = new DataTableBuilder("b");
        b.AddColumn(new object?[] { "C1", "C2", "C9" }, "CustomerId", typeof(string));
        b.AddColumn(new object?[] { "x", "y", "z" }, "Tag", typeof(string));
        var ctx = new FakeEvalContext();
        var result = new TableJoinNode().EvalTable(ctx,
            [TablesTestHelpers.Customers(), b.Build()], ("aKey", "CustomerId"), ("mode", "full"));
        Assert.That(result.Rows, Has.Count.EqualTo(4), "3 a rows plus unmatched C9");
        Assert.That(result.Cell("CustomerId", 3), Is.EqualTo("C9"));
    }

    [Test]
    public void Join_Semi_DuplicateBKeys_StillWarn()
    {
        var b = new DataTableBuilder("b");
        b.AddColumn(new object?[] { "C1", "C1" }, "CustomerId", typeof(string));
        var ctx = new FakeEvalContext();
        var result = new TableJoinNode().EvalTable(ctx,
            [TablesTestHelpers.Orders(), b.Build()], ("aKey", "CustomerId"), ("mode", "semi"));
        Assert.That(result.Rows, Has.Count.EqualTo(1));
        Assert.That(ctx.Warnings, Has.One.Contains("duplicate keys in b"));
    }

    [Test]
    public void Join_UnknownMode_Throws()
        => Assert.That(() => new TableJoinNode().EvalTable(
                [TablesTestHelpers.Orders(), TablesTestHelpers.Customers()],
                ("aKey", "CustomerId"), ("mode", "sideways")),
            Throws.ArgumentException.With.Message.StartsWith("table.join: ").And.Message.Contains("mode"));

    [Test]
    public void Join_BKeyDefaultsToAKey_AndDifferentBKeyWorks()
    {
        var b = new DataTableBuilder("b");
        b.AddColumn(new object?[] { "C1", "C2" }, "Code", typeof(string));
        b.AddColumn(new object?[] { "North", "South" }, "Region", typeof(string));
        var result = new TableJoinNode().EvalTable(
            [TablesTestHelpers.Orders(), b.Build()],
            ("aKey", "CustomerId"), ("bKey", "Code"), ("mode", "inner"));
        Assert.That(result.ColumnNames(), Is.EqualTo(new[] { "Id", "CustomerId", "Region" }));
        Assert.That(result.Cell("Region", 0), Is.EqualTo("North"));
    }

    [Test]
    public void Join_DuplicateBKeys_FirstWins_AndWarns()
    {
        var b = new DataTableBuilder("b");
        b.AddColumn(new object?[] { "C1", "C1" }, "CustomerId", typeof(string));
        b.AddColumn(new object?[] { "first", "second" }, "Tag", typeof(string));
        var ctx = new FakeEvalContext();
        var result = new TableJoinNode().EvalTable(ctx,
            [TablesTestHelpers.Orders(), b.Build()], ("aKey", "CustomerId"), ("mode", "inner"));
        Assert.That(result.Rows, Has.Count.EqualTo(1));
        Assert.That(result.Cell("Tag", 0), Is.EqualTo("first"));
        Assert.That(ctx.Warnings, Has.One.Contains("duplicate keys in b"));
    }

    [Test]
    public void Join_CollidingBColumn_LandsWithSuffix()
    {
        var b = new DataTableBuilder("b");
        b.AddColumn(new object?[] { "C1" }, "CustomerId", typeof(string));
        b.AddColumn(new object?[] { 99L }, "Id", typeof(long));
        var result = new TableJoinNode().EvalTable(
            [TablesTestHelpers.Orders(), b.Build()], ("aKey", "CustomerId"), ("mode", "inner"));
        Assert.That(result.ColumnNames(), Is.EqualTo(new[] { "Id", "CustomerId", "Id_b" }));
        Assert.That(result.Cell("Id_b", 0), Is.EqualTo(99L));
    }

    [Test]
    public void Join_KeysCompareAsCanonicalText_AcrossTypes()
    {
        var a = new DataTableBuilder("a");
        a.AddColumn(new object?[] { 1L, 2L }, "K", typeof(long));
        var b = new DataTableBuilder("b");
        b.AddColumn(new object?[] { " 1 ", "3" }, "K", typeof(string));
        b.AddColumn(new object?[] { "yes", "no" }, "Hit", typeof(string));
        var result = new TableJoinNode().EvalTable([a.Build(), b.Build()], ("aKey", "K"), ("mode", "inner"));
        Assert.That(result.Rows, Has.Count.EqualTo(1));
        Assert.That(result.Cell("Hit", 0), Is.EqualTo("yes"));
    }

    [Test]
    public void Join_MissingRequirements_Throw()
    {
        Assert.That(() => new TableJoinNode().EvalTable(
                [TablesTestHelpers.Orders(), TablesTestHelpers.Customers()]),
            Throws.ArgumentException.With.Message.Contains("aKey"));
        Assert.That(() => new TableJoinNode().EvalTable(
                [TablesTestHelpers.Orders(), TablesTestHelpers.Customers()], ("aKey", "NoSuchColumn")),
            Throws.ArgumentException.With.Message.Contains("NoSuchColumn"));
        Assert.That(() => new TableJoinNode().Eval(new FakeEvalContext(), [],
                NodeTestHelpers.Params(("aKey", "CustomerId"))),
            Throws.ArgumentException.With.Message.Contains("Table"));
    }

    [Test]
    public void SetOp_Intersect_KeepsARowsWithKeyInB_NullKeyDropped()
    {
        var result = new TableSetOpNode().EvalTable(
            [TablesTestHelpers.Orders(), TablesTestHelpers.Customers()],
            ("op", "intersect"), ("key", "CustomerId"));
        Assert.That(result.Rows, Has.Count.EqualTo(2));
        Assert.That(result.Cell("Id", 0), Is.EqualTo(1L));
        Assert.That(result.Cell("Id", 1), Is.EqualTo(2L));
        Assert.That(result.ColumnNames(), Is.EqualTo(new[] { "Id", "CustomerId" }));
    }

    [Test]
    public void SetOp_Subtract_KeepsARowsWithKeyNotInB_NullKeyKept()
    {
        var result = new TableSetOpNode().EvalTable(
            [TablesTestHelpers.Orders(), TablesTestHelpers.Customers()],
            ("op", "subtract"), ("key", "CustomerId"));
        Assert.That(result.Rows, Has.Count.EqualTo(2));
        Assert.That(result.Cell("CustomerId", 0), Is.Null);
        Assert.That(result.Cell("CustomerId", 1), Is.EqualTo("C9"));
    }

    [Test]
    public void SetOp_Union_AppendsBRowsWithAbsentKeys()
    {
        var b = new DataTableBuilder("more");
        b.AddColumn(new object?[] { 10L, 11L }, "Id", typeof(long));
        b.AddColumn(new object?[] { "C1", "C7" }, "CustomerId", typeof(string));
        var result = new TableSetOpNode().EvalTable(
            [TablesTestHelpers.Orders(), b.Build()], ("op", "union"), ("key", "CustomerId"));
        Assert.That(result.Rows, Has.Count.EqualTo(5), "C1 already present; only C7 appended");
        Assert.That(result.Cell("Id", 4), Is.EqualTo(11L));
        Assert.That(result.Cell("CustomerId", 4), Is.EqualTo("C7"));
    }

    [Test]
    public void SetOp_Union_ColumnMismatch_Throws()
        => Assert.That(() => new TableSetOpNode().EvalTable(
                [TablesTestHelpers.Orders(), TablesTestHelpers.Customers()],
                ("op", "union"), ("key", "CustomerId")),
            Throws.ArgumentException.With.Message.Contains("Id").And.Message.Contains("Name"));

    [Test]
    public void SetOp_UnknownOpOrKey_Throws()
    {
        Assert.That(() => new TableSetOpNode().EvalTable(
                [TablesTestHelpers.Orders(), TablesTestHelpers.Customers()],
                ("op", "sideways"), ("key", "CustomerId")),
            Throws.ArgumentException.With.Message.Contains("op"));
        Assert.That(() => new TableSetOpNode().EvalTable(
                [TablesTestHelpers.Orders(), TablesTestHelpers.Customers()],
                ("op", "intersect"), ("key", "NoSuchColumn")),
            Throws.ArgumentException.With.Message.Contains("NoSuchColumn"));
    }

    [Test]
    public void Project_KeepsColumnsInGivenOrder()
    {
        var result = new TableProjectNode().EvalTable(
            [TablesTestHelpers.Customers()], ("columns", "Name, CustomerId"));
        Assert.That(result.ColumnNames(), Is.EqualTo(new[] { "Name", "CustomerId" }));
        Assert.That(result.Rows, Has.Count.EqualTo(3));
        Assert.That(result.Cell("Name", 0), Is.EqualTo("Alice"));
    }

    [Test]
    public void Project_UnknownColumn_WarnsAndKeepsTheRest()
    {
        var ctx = new FakeEvalContext();
        var result = new TableProjectNode().EvalTable(ctx,
            [TablesTestHelpers.Customers()], ("columns", "Name, NoSuchColumn"));
        Assert.That(result.ColumnNames(), Is.EqualTo(new[] { "Name" }));
        Assert.That(ctx.Warnings, Has.One.Contains("NoSuchColumn"));
    }

    [Test]
    public void Project_EmptyColumns_Throws()
    {
        Assert.That(() => new TableProjectNode().EvalTable([TablesTestHelpers.Customers()], ("columns", "")),
            Throws.ArgumentException.With.Message.Contains("columns"));
        Assert.That(() => new TableProjectNode().EvalTable([TablesTestHelpers.Customers()], ("columns", ",")),
            Throws.ArgumentException.With.Message.Contains("columns"));
    }
}
