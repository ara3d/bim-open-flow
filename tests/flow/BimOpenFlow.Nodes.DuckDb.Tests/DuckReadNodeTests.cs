using Ara3D.BimOpenSchema.DuckDb;

namespace BimOpenFlow.Nodes.DuckDb.Tests;

[TestFixture]
public sealed class DuckReadNodeTests
{
    private string _folder = null!;
    private string _parquetPath = null!;
    private string _jsonPath = null!;

    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        _folder = Path.Combine(Path.GetTempPath(), "bimopenflow-duckdb-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_folder);
        _parquetPath = Path.Combine(_folder, "products.parquet");
        _jsonPath = Path.Combine(_folder, "products.json");

        var csv = NodeTestHelpers.SamplePath("products.csv").ToSqlLiteral();
        using var conn = BosDuckDb.OpenInMemory();
        conn.Execute($"COPY (SELECT * FROM read_csv_auto('{csv}')) TO '{_parquetPath.ToSqlLiteral()}' (FORMAT PARQUET)");
        conn.Execute($"COPY (SELECT * FROM read_csv_auto('{csv}')) TO '{_jsonPath.ToSqlLiteral()}' (FORMAT JSON)");
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
    public void Read_Csv_Auto()
    {
        var table = new DuckReadNode().EvalTable([], ("path", NodeTestHelpers.SamplePath("customers.csv")));
        Assert.That(table.ColumnNames(), Is.EqualTo(new[] { "CustomerId", "Name", "City", "Segment" }));
        Assert.That(table.Rows, Is.Not.Empty);
        Assert.That(table.Cell("CustomerId", 0), Is.EqualTo("C001"));
    }

    [Test]
    public void Read_Parquet_Auto()
    {
        var table = new DuckReadNode().EvalTable([], ("path", _parquetPath));
        Assert.That(table.ColumnNames(), Does.Contain("ProductId").And.Contain("UnitPrice"));
        Assert.That(table.Cell("ProductId", 0), Is.EqualTo("P-10"));
    }

    [Test]
    public void Read_Json_ExplicitFormat()
    {
        var table = new DuckReadNode().EvalTable([], ("path", _jsonPath), ("format", "json"));
        Assert.That(table.ColumnNames(), Does.Contain("ProductName"));
        Assert.That(table.Rows, Is.Not.Empty);
    }

    [Test]
    public void Read_SameContent_ReturnsSameTableInstance()
    {
        var original = NodeTestHelpers.SamplePath("orders.csv");
        var copy = Path.Combine(_folder, "orders-copy.csv");
        File.Copy(original, copy, overwrite: true);

        var first = new DuckReadNode().EvalTable([], ("path", original));
        var second = new DuckReadNode().EvalTable([], ("path", copy));
        Assert.That(ReferenceEquals(first, second), Is.True,
            "Same content (even at a different path) should hit the cache.");
    }

    [Test]
    public void Read_MissingFile_Throws()
        => Assert.That(
            () => new DuckReadNode().EvalTable([], ("path", Path.Combine(_folder, "absent.csv"))),
            Throws.InstanceOf<FileNotFoundException>().With.Message.Contains("duck.read"));

    [Test]
    public void Read_MissingPathParameter_Throws()
        => Assert.That(() => new DuckReadNode().EvalTable([]),
            Throws.ArgumentException.With.Message.Contains("duck.read"));

    [Test]
    public void Read_UnknownExtension_Throws()
    {
        var odd = Path.Combine(_folder, "data.unknown");
        File.WriteAllText(odd, "x");
        Assert.That(() => new DuckReadNode().EvalTable([], ("path", odd)),
            Throws.ArgumentException.With.Message.Contains("duck.read"));
    }

    [Test]
    public void Read_UnknownFormat_Throws()
        => Assert.That(
            () => new DuckReadNode().EvalTable([],
                ("path", NodeTestHelpers.SamplePath("customers.csv")), ("format", "xlsx")),
            Throws.ArgumentException.With.Message.Contains("duck.read"));
}
