namespace BimOpenFlow.Nodes.DuckDb.Tests;

[TestFixture]
public sealed class JsonReadNodeTests
{
    [Test]
    public void Read_Records()
    {
        var table = new JsonReadNode().EvalTable([],
            ("path", NodeTestHelpers.SamplePath("sample.json")), ("layout", "records"));
        Assert.That(table.ColumnNames(), Is.EqualTo(new[] { "id", "name", "geometry" }));
        Assert.That(table.Rows, Has.Count.EqualTo(3));
        Assert.That(table.Cell("name", 0), Is.EqualTo("Wall-A"));
    }

    [Test]
    public void Read_AutoLayout()
    {
        var table = new JsonReadNode().EvalTable([], ("path", NodeTestHelpers.SamplePath("sample.json")));
        Assert.That(table.Rows, Has.Count.EqualTo(3));
    }

    [Test]
    public void Read_Lines()
    {
        var table = new JsonReadNode().EvalTable([],
            ("path", NodeTestHelpers.SamplePath("lines.jsonl")), ("layout", "lines"));
        Assert.That(table.ColumnNames(), Is.EqualTo(new[] { "event", "count" }));
        Assert.That(table.Rows, Has.Count.EqualTo(3));
        Assert.That(table.Cell("event", 1), Is.EqualTo("modified"));
        Assert.That(table.Cell("count", 1), Is.EqualTo(5));
    }

    [Test]
    public void Read_Flatten_ExpandsNestedObjectToDottedColumns()
    {
        var table = new JsonReadNode().EvalTable([],
            ("path", NodeTestHelpers.SamplePath("sample.json")), ("flatten", "true"));
        Assert.That(table.ColumnNames(),
            Is.EqualTo(new[] { "id", "name", "geometry.height", "geometry.width" }));
        Assert.That(table.Cell("geometry.height", 0), Is.EqualTo(2.5));
        Assert.That(table.Cell("geometry.width", 2), Is.EqualTo(0.9));
    }

    [Test]
    public void Read_FlattenWithoutNesting_LeavesColumnsAlone()
    {
        var table = new JsonReadNode().EvalTable([],
            ("path", NodeTestHelpers.SamplePath("lines.jsonl")), ("flatten", "true"));
        Assert.That(table.ColumnNames(), Is.EqualTo(new[] { "event", "count" }));
        Assert.That(table.Rows, Has.Count.EqualTo(3));
    }

    [Test]
    public void Read_SameContent_ReturnsSameTableInstance()
    {
        var path = NodeTestHelpers.SamplePath("sample.json");
        var first = new JsonReadNode().EvalTable([], ("path", path));
        var second = new JsonReadNode().EvalTable([], ("path", path));
        Assert.That(ReferenceEquals(first, second), Is.True);
    }

    [Test]
    public void Read_MissingFile_Throws()
        => Assert.That(
            () => new JsonReadNode().EvalTable([], ("path", Path.Combine(Path.GetTempPath(), "absent.json"))),
            Throws.InstanceOf<FileNotFoundException>().With.Message.Contains("json.read"));

    [Test]
    public void Read_UnknownLayout_Throws()
        => Assert.That(
            () => new JsonReadNode().EvalTable([],
                ("path", NodeTestHelpers.SamplePath("sample.json")), ("layout", "table")),
            Throws.ArgumentException.With.Message.Contains("json.read"));

    [Test]
    public void Read_MissingPathParameter_Throws()
        => Assert.That(() => new JsonReadNode().EvalTable([]),
            Throws.ArgumentException.With.Message.Contains("json.read"));
}
