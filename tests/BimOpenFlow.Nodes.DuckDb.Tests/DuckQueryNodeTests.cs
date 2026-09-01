using Ara3D.BimOpenSchema.DuckDb;
using Ara3D.Utils;
using DuckDB.NET.Data;

namespace BimOpenFlow.Nodes.DuckDb.Tests;

[TestFixture]
public sealed class DuckQueryNodeTests
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
    public void Query_ReturnsMaterializedTable()
    {
        var table = new DuckQueryNode().EvalTable([],
            ("path", _dbPath),
            ("sql", "SELECT Name, Height FROM walls WHERE Height > 2.2 ORDER BY Name"));
        Assert.That(table.ColumnNames(), Is.EqualTo(new[] { "Name", "Height" }));
        Assert.That(table.Rows, Has.Count.EqualTo(2));
        Assert.That(table.Cell("Name", 0), Is.EqualTo("Wall-1"));
    }

    [Test]
    public void Query_MissingFile_Throws()
        => Assert.That(
            () => new DuckQueryNode().EvalTable([],
                ("path", Path.Combine(_folder, "absent.duckdb")), ("sql", "SELECT 1")),
            Throws.InstanceOf<FileNotFoundException>().With.Message.Contains("duck.query"));

    [Test]
    public void Query_MissingSqlParameter_Throws()
        => Assert.That(() => new DuckQueryNode().EvalTable([], ("path", _dbPath)),
            Throws.ArgumentException.With.Message.Contains("duck.query"));

    [Test]
    public void Query_NonSelect_Rejected()
        => Assert.That(
            () => new DuckQueryNode().EvalTable([],
                ("path", _dbPath), ("sql", "DROP TABLE walls")),
            Throws.ArgumentException.With.Message.Contains("duck.query"));

    [Test]
    public void Query_BadColumn_Throws()
        => Assert.That(
            () => new DuckQueryNode().EvalTable([],
                ("path", _dbPath), ("sql", "SELECT NoSuchColumn FROM walls")),
            Throws.InstanceOf<DuckDBException>());
}
