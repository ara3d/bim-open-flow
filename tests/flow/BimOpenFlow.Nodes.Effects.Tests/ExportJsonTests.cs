using System.Text.Json.Nodes;
using BimOpenFlow.Nodes.Effects;
using static BimOpenFlow.Nodes.Effects.Tests.TestSupport;

namespace BimOpenFlow.Nodes.Effects.Tests;

public sealed class ExportJsonTests
{
    private string _dir = "";

    [SetUp]
    public void SetUp()
        => _dir = NewTempDir();

    [TearDown]
    public void TearDown()
        => DeleteTempDir(_dir);

    [Test]
    public void RecordsLayoutWritesJsonArray()
    {
        var path = Path.Combine(_dir, "out.json");
        var outputs = new ExportJsonNode().Eval(FakeContext.Run, TableInput(FixtureTable()), Params(("path", path)));

        var records = JsonNode.Parse(File.ReadAllText(path))!.AsArray();
        Assert.That(records.Count, Is.EqualTo(3));
        Assert.That((string?)records[0]!["name"], Is.EqualTo("plain"));
        Assert.That((long?)records[0]!["count"], Is.EqualTo(1L));
        Assert.That((double?)records[0]!["ratio"], Is.EqualTo(0.5));
        Assert.That((bool?)records[0]!["flag"], Is.True);
        Assert.That(records[1]!["count"], Is.Null);
        Assert.That(Cell(OutputTable(outputs), "rowCount"), Is.EqualTo(3L));
    }

    [Test]
    public void LinesLayoutWritesOneObjectPerLine()
    {
        var path = Path.Combine(_dir, "out.jsonl");
        new ExportJsonNode().Eval(FakeContext.Run, TableInput(FixtureTable()),
            Params(("path", path), ("layout", "lines")));

        var lines = File.ReadAllLines(path).Where(l => l.Length > 0).ToArray();
        Assert.That(lines.Length, Is.EqualTo(3));
        foreach (var line in lines)
            Assert.That(JsonNode.Parse(line), Is.Not.Null);
        Assert.That((string?)JsonNode.Parse(lines[0])!["name"], Is.EqualTo("plain"));
    }

    [Test]
    public void IndentedRecordsAreIndentedAndStillParse()
    {
        var path = Path.Combine(_dir, "out.json");
        new ExportJsonNode().Eval(FakeContext.Run, TableInput(FixtureTable()),
            Params(("path", path), ("indent", "true")));

        var text = File.ReadAllText(path);
        Assert.That(text, Does.Contain("\n  "));
        Assert.That(JsonNode.Parse(text)!.AsArray().Count, Is.EqualTo(3));
    }

    [Test]
    public void IndentWithLinesLayoutThrowsWithKindPrefix()
        => Assert.That(
            Assert.Throws<ArgumentException>(() =>
                new ExportJsonNode().Eval(FakeContext.Run, TableInput(FixtureTable()),
                    Params(("path", Path.Combine(_dir, "x.jsonl")), ("layout", "lines"), ("indent", "true"))))!.Message,
            Does.StartWith("sink.exportJson: "));

    [Test]
    public void BadLayoutThrowsWithKindPrefix()
        => Assert.That(
            Assert.Throws<ArgumentException>(() =>
                new ExportJsonNode().Eval(FakeContext.Run, TableInput(FixtureTable()),
                    Params(("path", Path.Combine(_dir, "x.json")), ("layout", "table"))))!.Message,
            Does.StartWith("sink.exportJson: "));
}
