using Ara3D.BimOpenSchema.DuckDb;
using Ara3D.Utils;

namespace BimOpenFlow.Nodes.DuckDb.Tests;

[TestFixture]
public sealed class DuckTableNodeTests
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
    public void Table_ReturnsAllRows()
    {
        var table = new DuckTableNode().EvalTable([], ("path", _dbPath), ("table", "walls"));
        Assert.That(table.ColumnNames(), Is.EqualTo(new[] { "Name", "Height" }));
        Assert.That(table.Rows, Has.Count.EqualTo(3));
        Assert.That(table.Cell("Name", 0), Is.EqualTo("Wall-1"));
        Assert.That(table.Cell("Height", 1), Is.EqualTo(3.0));
    }

    [Test]
    public void Table_UnknownTable_ThrowsNamingIt()
        => Assert.That(
            () => new DuckTableNode().EvalTable([], ("path", _dbPath), ("table", "floors")),
            Throws.ArgumentException.With.Message.Contains("duck.table")
                .And.Message.Contains("floors"));

    [Test]
    public void Table_MissingFile_Throws()
        => Assert.That(
            () => new DuckTableNode().EvalTable([],
                ("path", Path.Combine(_folder, "absent.duckdb")), ("table", "walls")),
            Throws.InstanceOf<FileNotFoundException>().With.Message.Contains("duck.table"));

    [Test]
    public void Table_MissingTableParameter_Throws()
        => Assert.That(() => new DuckTableNode().EvalTable([], ("path", _dbPath)),
            Throws.ArgumentException.With.Message.Contains("duck.table"));
}
