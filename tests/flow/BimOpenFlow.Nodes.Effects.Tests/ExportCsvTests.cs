using Ara3D.DataFlowEngine.Abstractions;
using BimOpenFlow.Nodes.Effects;
using static BimOpenFlow.Nodes.Effects.Tests.TestSupport;

namespace BimOpenFlow.Nodes.Effects.Tests;

public sealed class ExportCsvTests
{
    private string _dir = "";

    [SetUp]
    public void SetUp()
        => _dir = NewTempDir();

    [TearDown]
    public void TearDown()
        => DeleteTempDir(_dir);

    [Test]
    public void GoldenCsvWithQuotingAndNulls()
    {
        var path = Path.Combine(_dir, "out.csv");
        var outputs = new ExportCsvNode().Eval(
            FakeContext.Run, TableInput(FixtureTable()), Params(("path", path)));

        var expected =
            "name,count,ratio,flag\r\n" +
            "plain,1,0.5,true\r\n" +
            "\"with, comma\",,2.25,false\r\n" +
            "\"with \"\"quote\"\"\nand newline\",3,,\r\n";
        Assert.That(File.ReadAllText(path), Is.EqualTo(expected));

        var summary = OutputTable(outputs);
        Assert.That(summary.Rows.Count, Is.EqualTo(1));
        Assert.That(Cell(summary, "path"), Is.EqualTo(path));
        Assert.That(Cell(summary, "rowCount"), Is.EqualTo(3L));
    }

    [Test]
    public void SemicolonDelimiterQuotesAccordingly()
    {
        var path = Path.Combine(_dir, "out.csv");
        new ExportCsvNode().Eval(FakeContext.Run, TableInput(FixtureTable()),
            Params(("path", path), ("delimiter", ";")));

        var lines = File.ReadAllText(path).Split("\r\n");
        Assert.That(lines[0], Is.EqualTo("name;count;ratio;flag"));
        Assert.That(lines[1], Is.EqualTo("plain;1;0.5;true"));
        Assert.That(lines[2], Is.EqualTo("with, comma;;2.25;false"), "comma no longer forces quoting");
    }

    [Test]
    public void DelimiterInsideCellIsQuoted()
    {
        var path = Path.Combine(_dir, "out.csv");
        var table = new MemoryTable("t", new[]
        {
            new MemoryColumn("a", typeof(string), new object?[] { "x;y" }, 0),
        });
        new ExportCsvNode().Eval(FakeContext.Run, TableInput(table),
            Params(("path", path), ("delimiter", ";")));
        Assert.That(File.ReadAllText(path), Is.EqualTo("a\r\n\"x;y\"\r\n"));
    }

    [Test]
    public void HeaderFalseOmitsHeaderRow()
    {
        var path = Path.Combine(_dir, "out.csv");
        new ExportCsvNode().Eval(FakeContext.Run, TableInput(FixtureTable()),
            Params(("path", path), ("header", "false")));
        Assert.That(File.ReadAllText(path), Does.StartWith("plain,1,0.5,true\r\n"));
    }

    [Test]
    public void EmptyDelimiterThrowsWithKindPrefix()
        => Assert.That(
            Assert.Throws<ArgumentException>(() =>
                new ExportCsvNode().Eval(FakeContext.Run, TableInput(FixtureTable()),
                    Params(("path", Path.Combine(_dir, "x.csv")), ("delimiter", ""))))!.Message,
            Does.StartWith("sink.exportCsv: "));

    [Test]
    public void EmptyPathThrows()
        => Assert.Throws<ArgumentException>(() =>
            new ExportCsvNode().Eval(FakeContext.Run, TableInput(FixtureTable()), ParamValues.Empty));

    [Test]
    public void MissingTableInputThrows()
        => Assert.Throws<ArgumentException>(() =>
            new ExportCsvNode().Eval(FakeContext.Run, Array.Empty<FlowValue>(),
                Params(("path", Path.Combine(_dir, "x.csv")))));
}
