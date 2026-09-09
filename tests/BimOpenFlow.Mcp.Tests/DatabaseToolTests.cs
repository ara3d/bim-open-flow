using System.Text.Json;
using DuckDB.NET.Data;

namespace BimOpenFlow.Mcp.Tests;

/// <summary>listDatabases and describeDatabase over a small database written
/// into the fixture's model root.</summary>
public sealed class DatabaseToolTests : FlowToolFixture
{
    private string _database = null!;

    [SetUp]
    public void WriteDatabase()
    {
        _database = Path.Combine(Root, "models", "sample.duckdb");
        using var conn = new DuckDBConnection($"DataSource={_database}");
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "CREATE TABLE door (id INTEGER, mark VARCHAR, width DOUBLE); "
            + "INSERT INTO door VALUES (1, 'D1', 0.9), (2, 'D2', NULL); "
            + "CREATE TABLE storey (id INTEGER, name VARCHAR)";
        cmd.ExecuteNonQuery();
    }

    [Test]
    public void ListDatabases_FindsTheFileUnderTheModelRoot()
    {
        var list = Json(FlowDatabaseTools.ListDatabases(Services));
        Assert.That(list.GetArrayLength(), Is.EqualTo(1));
        var entry = list[0];
        Assert.That(entry.GetProperty("name").GetString(), Is.EqualTo("sample.duckdb"));
        Assert.That(entry.GetProperty("path").GetString(), Does.Not.Contain('\\'));
        Assert.That(entry.GetProperty("sizeBytes").GetInt64(), Is.GreaterThan(0));
    }

    [Test]
    public void DescribeDatabase_ListsTablesColumnsAndRowCounts()
    {
        var description = Json(FlowDatabaseTools.DescribeDatabase(_database));
        var tables = description.GetProperty("tables").EnumerateArray().ToList();
        Assert.That(tables.Select(t => t.GetProperty("name").GetString()), Is.EqualTo(new[] { "door", "storey" }));
        var door = tables[0];
        Assert.That(door.GetProperty("rowCount").GetInt64(), Is.EqualTo(2));
        var columns = door.GetProperty("columns").EnumerateArray()
            .Select(c => (c.GetProperty("name").GetString(), c.GetProperty("type").GetString()))
            .ToList();
        Assert.That(columns, Is.EqualTo(new[] { ("id", "INTEGER"), ("mark", "VARCHAR"), ("width", "DOUBLE") }));
        Assert.That(tables[1].GetProperty("rowCount").GetInt64(), Is.EqualTo(0));
    }

    [Test]
    public void DescribeDatabase_MissingFileIsAnError()
        => Assert.Throws<FileNotFoundException>(
            () => FlowDatabaseTools.DescribeDatabase(Path.Combine(Root, "missing.duckdb")));
}
