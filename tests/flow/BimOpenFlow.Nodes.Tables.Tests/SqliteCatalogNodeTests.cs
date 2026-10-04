using Microsoft.Data.Sqlite;

namespace BimOpenFlow.Nodes.Tables.Tests;

/// <summary>sqlite.table and sqlite.tables against a generated database:
/// whole-table reads, catalog listing, and errors.</summary>
[TestFixture]
public sealed class SqliteCatalogNodeTests
{
    private string _dir = "";
    private string _path = "";

    [OneTimeSetUp]
    public void CreateDatabase()
    {
        _dir = Directory.CreateTempSubdirectory("tables-sqlite-catalog-").FullName;
        _path = Path.Combine(_dir, "test.db");
        using var connection = new SqliteConnection($"Data Source={_path}");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE items (name TEXT, qty INTEGER, price REAL);
            INSERT INTO items VALUES ('bolt', 10, 0.25), ('nut', 20, 0.10);
            CREATE TABLE empty (id INTEGER);
            """;
        command.ExecuteNonQuery();
    }

    [OneTimeTearDown]
    public void DeleteDatabase()
    {
        SqliteConnection.ClearAllPools();
        Directory.Delete(_dir, recursive: true);
    }

    [Test]
    public void Table_ReadsWholeTableWithUnifiedTypes()
    {
        var table = new SqliteTableNode().EvalTable([], ("path", _path), ("table", "items"));
        Assert.That(table.ColumnNames(), Is.EqualTo(new[] { "name", "qty", "price" }));
        Assert.That(table.Rows, Has.Count.EqualTo(2));
        Assert.That(table.Cell("name", 0), Is.EqualTo("bolt"));
        Assert.That(table.Cell("qty", 1), Is.EqualTo(20L));
        Assert.That(table.Columns[1].Descriptor.Type, Is.EqualTo(typeof(long)));
        Assert.That(table.Columns[2].Descriptor.Type, Is.EqualTo(typeof(double)));
    }

    [Test]
    public void Table_NameMatchesCaseInsensitively()
    {
        var table = new SqliteTableNode().EvalTable([], ("path", _path), ("table", "ITEMS"));
        Assert.That(table.Rows, Has.Count.EqualTo(2));
    }

    [Test]
    public void Table_UnknownTable_ThrowsNamingIt()
        => Assert.That(() => new SqliteTableNode().EvalTable([], ("path", _path), ("table", "nope")),
            Throws.ArgumentException.With.Message.StartsWith("sqlite.table: ").And.Message.Contains("nope"));

    [Test]
    public void Table_MissingFileOrParams_Throw()
    {
        Assert.That(() => new SqliteTableNode().EvalTable([],
                ("path", Path.Combine(_dir, "nope.db")), ("table", "items")),
            Throws.InstanceOf<FileNotFoundException>());
        Assert.That(() => new SqliteTableNode().EvalTable([], ("path", _path)),
            Throws.ArgumentException.With.Message.Contains("table"));
    }

    [Test]
    public void Tables_ListsUserTablesWithCounts()
    {
        var table = new SqliteTablesNode().EvalTable([], ("path", _path));
        Assert.That(table.ColumnNames(), Is.EqualTo(new[] { "name", "columnCount", "rowCount" }));
        Assert.That(table.Rows, Has.Count.EqualTo(2));
        Assert.That(table.Cell("name", 0), Is.EqualTo("empty"));
        Assert.That(table.Cell("columnCount", 0), Is.EqualTo(1L));
        Assert.That(table.Cell("rowCount", 0), Is.EqualTo(0L));
        Assert.That(table.Cell("name", 1), Is.EqualTo("items"));
        Assert.That(table.Cell("columnCount", 1), Is.EqualTo(3L));
        Assert.That(table.Cell("rowCount", 1), Is.EqualTo(2L));
    }

    [Test]
    public void Tables_MissingFileOrPath_Throw()
    {
        Assert.That(() => new SqliteTablesNode().EvalTable([], ("path", Path.Combine(_dir, "nope.db"))),
            Throws.InstanceOf<FileNotFoundException>().With.Message.StartsWith("sqlite.tables: "));
        Assert.That(() => new SqliteTablesNode().EvalTable([]),
            Throws.ArgumentException.With.Message.StartsWith("sqlite.tables: "));
    }
}
