using Ara3D.BimOpenSchema.DuckDb;
using BimOpenFlow.Nodes.Effects;
using static BimOpenFlow.Nodes.Effects.Tests.TestSupport;

namespace BimOpenFlow.Nodes.Effects.Tests;

public sealed class ExportParquetTests
{
    private string _dir = "";

    [SetUp]
    public void SetUp()
        => _dir = NewTempDir();

    [TearDown]
    public void TearDown()
        => DeleteTempDir(_dir);

    [TestCase("zstd")]
    [TestCase("snappy")]
    [TestCase("none")]
    public void RoundTripsThroughDuckDb(string compression)
    {
        var path = Path.Combine(_dir, "out.parquet");
        var outputs = new ExportParquetNode().Eval(FakeContext.Run, TableInput(FixtureTable()),
            Params(("path", path), ("compression", compression)));

        using var conn = BosDuckDb.OpenInMemory();
        var table = conn.Query($"SELECT * FROM read_parquet('{path.Replace('\\', '/')}') ORDER BY count NULLS LAST");
        Assert.That(table.Columns.Select(c => c.Descriptor.Name), Is.EqualTo(new[] { "name", "count", "ratio", "flag" }));
        Assert.That(table.Rows.Count, Is.EqualTo(3));
        Assert.That(Cell(table, "name"), Is.EqualTo("plain"));
        Assert.That(Cell(table, "count"), Is.EqualTo(1L));
        Assert.That(Cell(table, "ratio", 1), Is.Null, "ordered second row is the count=3 fixture row");
        Assert.That(Cell(table, "flag"), Is.True);
        Assert.That(Cell(table, "count", 2), Is.Null);

        var summary = OutputTable(outputs);
        Assert.That(Cell(summary, "path"), Is.EqualTo(path));
        Assert.That(Cell(summary, "rowCount"), Is.EqualTo(3L));
    }

    [Test]
    public void ReplacesExistingFile()
    {
        var path = Path.Combine(_dir, "out.parquet");
        File.WriteAllText(path, "not parquet");
        new ExportParquetNode().Eval(FakeContext.Run, TableInput(FixtureTable()), Params(("path", path)));
        using var conn = BosDuckDb.OpenInMemory();
        Assert.That(conn.ScalarInt64($"SELECT count(*) FROM read_parquet('{path.Replace('\\', '/')}')"), Is.EqualTo(3));
    }

    [Test]
    public void BadCompressionThrowsWithKindPrefix()
        => Assert.That(
            Assert.Throws<ArgumentException>(() =>
                new ExportParquetNode().Eval(FakeContext.Run, TableInput(FixtureTable()),
                    Params(("path", Path.Combine(_dir, "x.parquet")), ("compression", "gzip"))))!.Message,
            Does.StartWith("sink.exportParquet: "));
}
