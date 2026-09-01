namespace BimOpenFlow.Nodes.Tables.Tests;

/// <summary>table.inline: type inference from JSON values, null and missing
/// keys, heterogeneous-column errors, and malformed input.</summary>
[TestFixture]
public sealed class TableInlineNodeTests
{
    [Test]
    public void Inline_InfersColumnTypesFromValues()
    {
        var table = new TableInlineNode().EvalTable([], ("rows",
            """[{"type":"Wall","rate":120.5,"qty":2,"active":true},{"type":"Door","rate":80.0,"qty":5,"active":false}]"""));
        Assert.That(table.ColumnNames(), Is.EqualTo(new[] { "type", "rate", "qty", "active" }));
        Assert.That(table.Rows, Has.Count.EqualTo(2));
        Assert.That(table.Cell("type", 0), Is.EqualTo("Wall"));
        Assert.That(table.Cell("rate", 0), Is.EqualTo(120.5));
        Assert.That(table.Cell("qty", 1), Is.EqualTo(5L));
        Assert.That(table.Cell("active", 1), Is.EqualTo(false));
        Assert.That(table.Columns[0].Descriptor.Type, Is.EqualTo(typeof(string)));
        Assert.That(table.Columns[1].Descriptor.Type, Is.EqualTo(typeof(double)));
        Assert.That(table.Columns[2].Descriptor.Type, Is.EqualTo(typeof(long)));
        Assert.That(table.Columns[3].Descriptor.Type, Is.EqualTo(typeof(bool)));
    }

    [Test]
    public void Inline_NullsAndMissingKeys_LandAsNulls()
    {
        var table = new TableInlineNode().EvalTable([], ("rows",
            """[{"a":1,"b":null},{"b":"x","c":true}]"""));
        Assert.That(table.ColumnNames(), Is.EqualTo(new[] { "a", "b", "c" }));
        Assert.That(table.Cell("a", 1), Is.Null, "missing key");
        Assert.That(table.Cell("b", 0), Is.Null, "explicit null");
        Assert.That(table.Cell("c", 0), Is.Null, "column discovered in a later row");
        Assert.That(table.Cell("c", 1), Is.EqualTo(true));
    }

    [Test]
    public void Inline_EmptyArray_YieldsEmptyTableWithNoColumns()
    {
        var table = new TableInlineNode().EvalTable([], ("rows", "[]"));
        Assert.That(table.Columns, Is.Empty);
        Assert.That(table.Rows, Is.Empty);
    }

    [Test]
    public void Inline_HeterogeneousColumn_ThrowsNamingIt()
    {
        Assert.That(() => new TableInlineNode().EvalTable([], ("rows", """[{"x":1},{"x":"a"}]""")),
            Throws.ArgumentException.With.Message.StartsWith("table.inline: ").And.Message.Contains("'x'"));
    }

    [Test]
    public void Inline_IntegerAndNumber_WidenToNumber()
    {
        // JSON has one number type: "120" next to "120.5" is the same column.
        var table = new TableInlineNode().EvalTable([], ("rows", """[{"x":1},{"x":1.5}]"""));
        Assert.That(table.Columns[0].Descriptor.Type, Is.EqualTo(typeof(double)));
        Assert.That(table.Cell("x", 0), Is.EqualTo(1.0));
        Assert.That(table.Cell("x", 1), Is.EqualTo(1.5));
    }

    [Test]
    public void Inline_MalformedInput_Throws()
    {
        Assert.That(() => new TableInlineNode().EvalTable([], ("rows", "{\"a\":1}")),
            Throws.ArgumentException.With.Message.StartsWith("table.inline: ").And.Message.Contains("array"));
        Assert.That(() => new TableInlineNode().EvalTable([], ("rows", "[1, 2]")),
            Throws.ArgumentException.With.Message.Contains("row 0"));
        Assert.That(() => new TableInlineNode().EvalTable([], ("rows", """[{"a":[1]}]""")),
            Throws.ArgumentException.With.Message.Contains("'a'"));
        Assert.That(() => new TableInlineNode().EvalTable([], ("rows", "not json")),
            Throws.ArgumentException.With.Message.StartsWith("table.inline: "));
        Assert.That(() => new TableInlineNode().EvalTable([]),
            Throws.ArgumentException.With.Message.Contains("rows"));
    }
}
