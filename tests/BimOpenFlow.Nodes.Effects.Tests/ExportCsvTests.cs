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
    public void EmptyPathThrows()
        => Assert.Throws<ArgumentException>(() =>
            new ExportCsvNode().Eval(FakeContext.Run, TableInput(FixtureTable()), ParamValues.Empty));

    [Test]
    public void MissingTableInputThrows()
        => Assert.Throws<ArgumentException>(() =>
            new ExportCsvNode().Eval(FakeContext.Run, Array.Empty<FlowValue>(),
                Params(("path", Path.Combine(_dir, "x.csv")))));
}
