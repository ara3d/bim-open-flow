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
        cmd.CommandText = "CREATE TABLE door (id INTEGER, mark VARCHAR, width DOUBLE, width_reason VARCHAR, width_evidence VARCHAR[]); "
            + "INSERT INTO door VALUES (1, 'D1', 0.9, NULL, NULL), (2, 'D2', NULL, 'NotObserved', NULL); "
            + "CREATE TABLE storey (id INTEGER, name VARCHAR)";
        cmd.ExecuteNonQuery();
    }

    [Test]
    public void DescribeDatabase_FoldsCompanionColumnsAndSkipsEmptyTableColumns()
    {
        var description = Json(FlowDatabaseTools.DescribeDatabase(_database));
        var tables = description.GetProperty("tables").EnumerateArray().ToList();
        var door = tables[0];
        Assert.That(door.GetProperty("columns").EnumerateArray().Select(c => c.GetString()),
            Is.EqualTo(new[] { "id", "mark", "width" }), "companion columns are folded away");
        Assert.That(door.GetProperty("companions").EnumerateArray().Select(c => c.GetString()), Is.EqualTo(new[] { "width" }));
        var storey = tables[1];
        Assert.That(storey.GetProperty("rowCount").GetInt64(), Is.EqualTo(0));
        Assert.That(storey.TryGetProperty("columns", out _), Is.False, "an empty table lists no columns");
        Assert.That(storey.GetProperty("columnCount").GetInt64(), Is.EqualTo(2));
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
    public void DescribeDatabase_SummarisesEveryTableWithColumnNames()
    {
        var description = Json(FlowDatabaseTools.DescribeDatabase(_database));
        var tables = description.GetProperty("tables").EnumerateArray().ToList();
        Assert.That(tables.Select(t => t.GetProperty("name").GetString()), Is.EqualTo(new[] { "door", "storey" }));
        var door = tables[0];
        Assert.That(door.GetProperty("rowCount").GetInt64(), Is.EqualTo(2));
        Assert.That(door.GetProperty("columns").EnumerateArray().Select(c => c.GetString()),
            Is.EqualTo(new[] { "id", "mark", "width" }));
        Assert.That(tables[1].GetProperty("rowCount").GetInt64(), Is.EqualTo(0));
    }

    [Test]
    public void DescribeDatabase_OneTableHasColumnTypes()
    {
        var description = Json(FlowDatabaseTools.DescribeDatabase(_database, "door"));
        var tables = description.GetProperty("tables").EnumerateArray().ToList();
        Assert.That(tables, Has.Count.EqualTo(1));
        var columns = tables[0].GetProperty("columns").EnumerateArray()
            .Select(c => (c.GetProperty("name").GetString(), c.GetProperty("type").GetString()))
            .ToList();
        Assert.That(columns, Is.EqualTo(new[]
        {
            ("id", "INTEGER"), ("mark", "VARCHAR"), ("width", "DOUBLE"), ("width_reason", "VARCHAR"), ("width_evidence", "VARCHAR[]"),
        }));
    }

    [Test]
    public void DescribeDatabase_OneTableProfilesValues()
    {
        var description = Json(FlowDatabaseTools.DescribeDatabase(_database, "door"));
        var columns = description.GetProperty("tables")[0].GetProperty("columns").EnumerateArray()
            .ToDictionary(c => c.GetProperty("name").GetString()!);
        var mark = columns["mark"];
        Assert.That(mark.GetProperty("samples").EnumerateArray().Select(v => v.GetString()), Is.EqualTo(new[] { "D1", "D2" }));
        Assert.That(mark.GetProperty("distinct").GetInt64(), Is.EqualTo(2));
        Assert.That(mark.GetProperty("nulls").GetInt64(), Is.EqualTo(0));
        var width = columns["width"];
        Assert.That(width.GetProperty("nulls").GetInt64(), Is.EqualTo(1));
        Assert.That(width.GetProperty("min").GetString(), Is.EqualTo("0.9"));
        Assert.That(width.GetProperty("max").GetString(), Is.EqualTo("0.9"));
        var reason = columns["width_reason"];
        Assert.That(reason.GetProperty("samples").EnumerateArray().Select(v => v.GetString()), Is.EqualTo(new[] { "NotObserved" }));
        var evidence = columns["width_evidence"];
        Assert.That(evidence.GetProperty("nulls").GetInt64(), Is.EqualTo(2), "list columns are profiled too");
        Assert.That(evidence.GetProperty("samples").GetArrayLength(), Is.EqualTo(0));

        var empty = Json(FlowDatabaseTools.DescribeDatabase(_database, "storey"));
        var storeyColumns = empty.GetProperty("tables")[0].GetProperty("columns")[0];
        Assert.That(storeyColumns.TryGetProperty("samples", out _), Is.False, "an empty table has only names and types");
    }

    [Test]
    public void DescribeDatabase_UnknownTableIsAnError()
        => Assert.Throws<ArgumentException>(() => FlowDatabaseTools.DescribeDatabase(_database, "wall"));

    [Test]
    public void DescribeDatabase_MissingFileIsAnError()
        => Assert.Throws<FileNotFoundException>(
            () => FlowDatabaseTools.DescribeDatabase(Path.Combine(Root, "missing.duckdb")));
}
