using Ara3D.BimOpenSchema.DuckDb;

namespace BimOpenFlow.Nodes.DuckDb.Tests;

[TestFixture]
public sealed class ParquetReadNodeTests
{
    private string _folder = null!;

    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        _folder = Path.Combine(Path.GetTempPath(), "bimopenflow-duckdb-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_folder);
        using var conn = BosDuckDb.OpenInMemory();
        foreach (var month in new[] { "monthly-2024-01", "monthly-2024-02" })
        {
            var csv = NodeTestHelpers.SamplePath($"{month}.csv").ToSqlLiteral();
            var parquet = Path.Combine(_folder, $"{month}.parquet").ToSqlLiteral();
            conn.Execute($"COPY (SELECT * FROM read_csv_auto('{csv}')) TO '{parquet}' (FORMAT PARQUET)");
        }
    }

    [OneTimeTearDown]
    public void OneTimeTearDown()
    {
        try
        {
            Directory.Delete(_folder, recursive: true);
        }
        catch (IOException)
        {
        }
    }

    [Test]
    public void Read_SingleFile()
    {
        var table = new ParquetReadNode().EvalTable([],
            ("path", Path.Combine(_folder, "monthly-2024-01.parquet")));
        Assert.That(table.ColumnNames(), Is.EqualTo(new[] { "Month", "Item", "Amount" }));
        Assert.That(table.Rows, Has.Count.EqualTo(3));
        Assert.That(table.Cell("Item", 0), Is.EqualTo("Concrete"));
    }

    [Test]
    public void Read_Glob_UnionsFiles()
    {
        var table = new ParquetReadNode().EvalTable([],
            ("path", Path.Combine(_folder, "monthly-*.parquet")));
        Assert.That(table.Rows, Has.Count.EqualTo(6));
        var months = Enumerable.Range(0, 6).Select(r => table.Cell("Month", r)).Distinct().ToList();
        Assert.That(months, Is.EquivalentTo(new[] { "2024-01", "2024-02" }));
    }

    [Test]
    public void Read_SameContent_ReturnsSameTableInstance()
    {
        var path = Path.Combine(_folder, "monthly-2024-02.parquet");
        var first = new ParquetReadNode().EvalTable([], ("path", path));
        var second = new ParquetReadNode().EvalTable([], ("path", path));
        Assert.That(ReferenceEquals(first, second), Is.True);
    }

    [Test]
    public void Read_MissingFile_Throws()
        => Assert.That(
            () => new ParquetReadNode().EvalTable([], ("path", Path.Combine(_folder, "absent.parquet"))),
            Throws.InstanceOf<FileNotFoundException>().With.Message.Contains("parquet.read"));

    [Test]
    public void Read_EmptyGlob_Throws()
        => Assert.That(
            () => new ParquetReadNode().EvalTable([], ("path", Path.Combine(_folder, "nothing-*.parquet"))),
            Throws.InstanceOf<FileNotFoundException>().With.Message.Contains("parquet.read"));

    [Test]
    public void Read_MissingPathParameter_Throws()
        => Assert.That(() => new ParquetReadNode().EvalTable([]),
            Throws.ArgumentException.With.Message.Contains("parquet.read"));
}
