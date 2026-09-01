using Microsoft.Data.Sqlite;

namespace BimOpenFlow.Nodes.Tables.Tests;

/// <summary>sqlite.query against a database generated into a temp directory:
/// materialization, nulls, single-SELECT validation, and errors.</summary>
[TestFixture]
public sealed class SqliteQueryNodeTests
{
    private string _dir = "";
    private string _path = "";

    [OneTimeSetUp]
    public void CreateDatabase()
    {
        _dir = Directory.CreateTempSubdirectory("tables-sqlite-").FullName;
        _path = Path.Combine(_dir, "test.db");
        using var connection = new SqliteConnection($"Data Source={_path}");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE items (name TEXT, qty INTEGER, price REAL);
            INSERT INTO items VALUES ('bolt', 10, 0.25), ('nut', 20, 0.10), (NULL, NULL, NULL);
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
    public void Query_MaterializesTypedColumnsAndNulls()
    {
        var table = new SqliteQueryNode().EvalTable([],
            ("path", _path), ("sql", "SELECT name, qty, price FROM items ORDER BY qty"));
        Assert.That(table.ColumnNames(), Is.EqualTo(new[] { "name", "qty", "price" }));
        Assert.That(table.Rows, Has.Count.EqualTo(3));
        Assert.That(table.Cell("name", 0), Is.Null);
        Assert.That(table.Cell("qty", 1), Is.EqualTo(10L));
        Assert.That(table.Cell("price", 2), Is.EqualTo(0.10));
        Assert.That(table.Columns[1].Descriptor.Type, Is.EqualTo(typeof(long)));
        Assert.That(table.Columns[2].Descriptor.Type, Is.EqualTo(typeof(double)));
    }

    [Test]
    public void Query_WithStatementAndTrailingSemicolon_Allowed()
    {
        var table = new SqliteQueryNode().EvalTable([],
            ("path", _path), ("sql", "WITH big AS (SELECT * FROM items WHERE qty > 15) SELECT name FROM big;  "));
        Assert.That(table.Rows, Has.Count.EqualTo(1));
        Assert.That(table.Cell("name", 0), Is.EqualTo("nut"));
    }

    [Test]
    public void Query_RejectsNonSelectAndMultipleStatements()
    {
        Assert.That(() => new SqliteQueryNode().EvalTable([],
                ("path", _path), ("sql", "DELETE FROM items")),
            Throws.ArgumentException.With.Message.Contains("SELECT"));
        Assert.That(() => new SqliteQueryNode().EvalTable([],
                ("path", _path), ("sql", "SELECT 1; DROP TABLE items")),
            Throws.ArgumentException.With.Message.Contains("single"));
    }

    [Test]
    public void Query_SqlErrors_SurfaceAsArgumentExceptions()
        => Assert.That(() => new SqliteQueryNode().EvalTable([],
                ("path", _path), ("sql", "SELECT nope FROM items")),
            Throws.ArgumentException.With.Message.Contains(SqliteQueryNode.Kind));

    [Test]
    public void Query_MissingFileOrParams_Throw()
    {
        Assert.That(() => new SqliteQueryNode().EvalTable([],
                ("path", Path.Combine(_dir, "nope.db")), ("sql", "SELECT 1")),
            Throws.InstanceOf<FileNotFoundException>());
        Assert.That(() => new SqliteQueryNode().EvalTable([], ("path", _path)),
            Throws.ArgumentException.With.Message.Contains("sql"));
    }
}
