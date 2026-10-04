using Ara3D.BimOpenSchema.DuckDb;
using Ara3D.Utils;

namespace BimOpenFlow.Nodes.DuckDb.Tests;

[TestFixture]
public sealed class DuckTablesNodeTests
{
    private string _folder = null!;
    private string _dbPath = null!;

    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        _folder = Path.Combine(Path.GetTempPath(), "bimopenflow-duckdb-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_folder);
        _dbPath = Path.Combine(_folder, "test.duckdb");
        using var conn = BosDuckDb.Open(new FilePath(_dbPath));
        conn.Execute("CREATE TABLE walls (Name VARCHAR, Height DOUBLE)");
        conn.Execute("INSERT INTO walls VALUES ('Wall-1', 2.5), ('Wall-2', 3.0), ('Wall-3', 2.1)");
        conn.Execute("CREATE TABLE doors (Name VARCHAR, Width DOUBLE, Level VARCHAR)");
        conn.Execute("INSERT INTO doors VALUES ('Door-1', 0.9, 'L1'), ('Door-2', 1.0, 'L2')");
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
    public void Tables_ListsNamesWithRealCounts()
    {
        var table = new DuckTablesNode().EvalTable([], ("path", _dbPath));
        Assert.That(table.ColumnNames(), Is.EqualTo(new[] { "name", "columnCount", "rowCount" }));
        Assert.That(table.Rows, Has.Count.EqualTo(2));
        Assert.That(table.Cell("name", 0), Is.EqualTo("doors"));
        Assert.That(table.Cell("columnCount", 0), Is.EqualTo(3));
        Assert.That(table.Cell("rowCount", 0), Is.EqualTo(2));
        Assert.That(table.Cell("name", 1), Is.EqualTo("walls"));
        Assert.That(table.Cell("columnCount", 1), Is.EqualTo(2));
        Assert.That(table.Cell("rowCount", 1), Is.EqualTo(3));
    }

    [Test]
    public void Tables_MissingFile_Throws()
        => Assert.That(
            () => new DuckTablesNode().EvalTable([], ("path", Path.Combine(_folder, "absent.duckdb"))),
            Throws.InstanceOf<FileNotFoundException>().With.Message.Contains("duck.tables"));

    [Test]
    public void Tables_MissingPathParameter_Throws()
        => Assert.That(() => new DuckTablesNode().EvalTable([]),
            Throws.ArgumentException.With.Message.Contains("duck.tables"));
}
