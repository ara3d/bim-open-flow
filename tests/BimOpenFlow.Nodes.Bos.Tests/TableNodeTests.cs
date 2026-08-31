using Ara3D.DataFlowEngine.Abstractions;

namespace BimOpenFlow.Nodes.Bos.Tests;

/// <summary>Every table node's happy path plus error surfacing and null handling,
/// on a small in-code table (no files, no sample data).</summary>
[TestFixture]
public sealed class TableNodeTests
{
    [Test]
    public void Pack_ExposesAllSixNodes()
        => Assert.That(BosNodes.All.Select(n => n.Spec.Kind), Is.EquivalentTo(new[]
        {
            "bos.load", "bos.query", "table.filter", "table.derive", "table.aggregate", "table.sort",
        }));

    [Test]
    public void Filter_KeepsMatchingRows_AndExcludesNulls()
    {
        var result = new TableFilterNode().EvalTable(NodeTestHelpers.SampleTable(), ("expr", "Height > 2.2"));
        Assert.That(result.Rows, Has.Count.EqualTo(2));
        Assert.That(result.Cell("Height", 0), Is.EqualTo(2.5));
        Assert.That(result.Cell("Height", 1), Is.EqualTo(3.0));
        Assert.That(result.ColumnNames(), Is.EqualTo(NodeTestHelpers.SampleTable().ColumnNames()));
    }

    [Test]
    public void Filter_NullResult_ExcludesTheRow()
    {
        var result = new TableFilterNode().EvalTable(NodeTestHelpers.SampleTable(), ("expr", "Height > 0"));
        Assert.That(result.Rows, Has.Count.EqualTo(3));
    }

    [Test]
    public void Filter_NonBooleanExpression_Throws()
        => Assert.That(
            () => new TableFilterNode().EvalTable(NodeTestHelpers.SampleTable(), ("expr", "Height + 1")),
            Throws.ArgumentException.With.Message.Contains("Boolean"));

    [Test]
    public void Filter_ParseAndTypeErrors_SurfaceWithOffsets()
    {
        Assert.That(
            () => new TableFilterNode().EvalTable(NodeTestHelpers.SampleTable(), ("expr", "Height >")),
            Throws.ArgumentException.With.Message.Contains("expr"));
        Assert.That(
            () => new TableFilterNode().EvalTable(NodeTestHelpers.SampleTable(), ("expr", "NoSuchColumn > 1")),
            Throws.ArgumentException.With.Message.Contains("NoSuchColumn"));
    }

    [Test]
    public void Derive_AddsComputedColumn_WithNullPropagation()
    {
        var result = new TableDeriveNode().EvalTable(NodeTestHelpers.SampleTable(),
            ("name", "Doubled"), ("expr", "Height * 2"));
        Assert.That(result.ColumnNames(), Does.Contain("Doubled"));
        Assert.That(result.Rows, Has.Count.EqualTo(4));
        Assert.That(result.Cell("Doubled", 0), Is.EqualTo(5.0));
        Assert.That(result.Cell("Doubled", 1), Is.Null);
        Assert.That(result.Cell("Doubled", 3), Is.EqualTo(6.0));
        Assert.That(result.Columns.Single(c => c.Descriptor.Name == "Doubled").Descriptor.Type,
            Is.EqualTo(typeof(double)));
    }

    [Test]
    public void Derive_TextConcat_PropagatesNullNames()
    {
        var result = new TableDeriveNode().EvalTable(NodeTestHelpers.SampleTable(),
            ("name", "Label"), ("expr", "Name & \"!\""));
        Assert.That(result.Cell("Label", 0), Is.EqualTo("Wall-1!"));
        Assert.That(result.Cell("Label", 3), Is.Null);
    }

    [Test]
    public void Derive_DuplicateColumnName_Throws()
        => Assert.That(
            () => new TableDeriveNode().EvalTable(NodeTestHelpers.SampleTable(),
                ("name", "Height"), ("expr", "1")),
            Throws.ArgumentException.With.Message.Contains("already exists"));

    [Test]
    public void Derive_TypeError_Throws()
        => Assert.That(
            () => new TableDeriveNode().EvalTable(NodeTestHelpers.SampleTable(),
                ("name", "Bad"), ("expr", "Height and true")),
            Throws.ArgumentException.With.Message.Contains("expr"));

    [Test]
    public void Aggregate_GroupsAndComputes()
    {
        var result = new TableAggregateNode().EvalTable(NodeTestHelpers.SampleTable(),
            ("groupBy", "Category"),
            ("aggregates", "count(*) as n, sum(Count) as total, avg(Count) as mean, max(Height) as tallest"));
        Assert.That(result.Rows, Has.Count.EqualTo(2));
        Assert.That(result.Cell("Category", 0), Is.EqualTo("Doors"));
        Assert.That(result.Cell("n", 0), Is.EqualTo(2L));
        Assert.That(result.Cell("total", 0), Is.EqualTo(7L));
        Assert.That(result.Cell("mean", 0), Is.EqualTo(3.5));
        Assert.That(result.Cell("tallest", 0), Is.EqualTo(3.0));
        Assert.That(result.Cell("Category", 1), Is.EqualTo("Walls"));
        Assert.That(result.Cell("total", 1), Is.EqualTo(3L));
    }

    [Test]
    public void Aggregate_EmptyGroupBy_YieldsOneRow()
    {
        var result = new TableAggregateNode().EvalTable(NodeTestHelpers.SampleTable(),
            ("groupBy", ""), ("aggregates", "count(Height) as withHeight, min(Count) as first"));
        Assert.That(result.Rows, Has.Count.EqualTo(1));
        Assert.That(result.Cell("withHeight", 0), Is.EqualTo(3L));
        Assert.That(result.Cell("first", 0), Is.EqualTo(1L));
    }

    [Test]
    public void Aggregate_RejectsBadSpecs()
    {
        var node = new TableAggregateNode();
        Assert.That(() => node.EvalTable(NodeTestHelpers.SampleTable(),
                ("groupBy", ""), ("aggregates", "median(Count) as m")),
            Throws.ArgumentException.With.Message.Contains("median"));
        Assert.That(() => node.EvalTable(NodeTestHelpers.SampleTable(),
                ("groupBy", ""), ("aggregates", "sum(NoSuchColumn) as s")),
            Throws.ArgumentException.With.Message.Contains("NoSuchColumn"));
        Assert.That(() => node.EvalTable(NodeTestHelpers.SampleTable(),
                ("groupBy", ""), ("aggregates", "sum(*) as s")),
            Throws.ArgumentException);
        Assert.That(() => node.EvalTable(NodeTestHelpers.SampleTable(),
                ("groupBy", "NoSuchColumn"), ("aggregates", "count(*) as n")),
            Throws.ArgumentException.With.Message.Contains("NoSuchColumn"));
    }

    [Test]
    public void Sort_SupportsDescendingAndMultipleKeys()
    {
        var result = new TableSortNode().EvalTable(NodeTestHelpers.SampleTable(),
            ("by", "Category, Count desc"));
        Assert.That(result.Cell("Count", 0), Is.EqualTo(4L));
        Assert.That(result.Cell("Count", 1), Is.EqualTo(3L));
        Assert.That(result.Cell("Count", 2), Is.EqualTo(2L));
        Assert.That(result.Cell("Count", 3), Is.EqualTo(1L));
    }

    [Test]
    public void Sort_RejectsUnknownColumnsAndBadTerms()
    {
        Assert.That(() => new TableSortNode().EvalTable(NodeTestHelpers.SampleTable(), ("by", "NoSuchColumn")),
            Throws.ArgumentException.With.Message.Contains("NoSuchColumn"));
        Assert.That(() => new TableSortNode().EvalTable(NodeTestHelpers.SampleTable(), ("by", "Count sideways")),
            Throws.ArgumentException);
    }

    [Test]
    public void Query_RunsReadOnlySqlOverT()
    {
        var result = new BosQueryNode().EvalTable(NodeTestHelpers.SampleTable(),
            ("sql", "SELECT Name, Count FROM t WHERE Count >= 2 ORDER BY Count DESC"));
        Assert.That(result.Rows, Has.Count.EqualTo(3));
        Assert.That(result.Cell("Count", 0), Is.EqualTo(4L));
        Assert.That(result.Cell("Name", 0), Is.Null);
    }

    [Test]
    public void Query_RejectsNonSelectStatements()
        => Assert.That(
            () => new BosQueryNode().EvalTable(NodeTestHelpers.SampleTable(), ("sql", "DELETE FROM t")),
            Throws.ArgumentException);

    [Test]
    public void TableNodes_RequireATableInput()
        => Assert.That(() => new TableFilterNode().Eval(NodeTestHelpers.Ctx, [], NodeTestHelpers.Params(("expr", "true"))),
            Throws.ArgumentException.With.Message.Contains("Table"));
}
