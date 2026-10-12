namespace BimOpenFlow.Relations.DuckDb.Tests;

[TestFixture]
public sealed class FileSchemaTests
{
    private Fixture _f = null!;
    [OneTimeSetUp] public void Up() => _f = new Fixture();
    [OneTimeTearDown] public void Down() => _f.Dispose();

    private string Db => Path.Combine(_f.Folder, "levels.duckdb");
    private string Absent => Path.Combine(_f.Folder, "absent.duckdb");

    private static IReadOnlyDictionary<string, string> Options(string key, string value)
        => new Dictionary<string, string> { [key] = value };

    [Test]
    public void FileQuerySchema_types_sql_that_reads_a_file()
    {
        var walls = Path.Combine(_f.Folder, "walls.csv").Replace(Path.DirectorySeparatorChar, '/');
        Assert.That(_f.Catalog.FileQuerySchema($"SELECT id, height FROM read_csv('{walls}')", [walls]).ToString(),
            Is.EqualTo("id:Integer?, height:Number?"));
    }

    [Test]
    public void DuckDbFile_table_and_query_are_described()
    {
        Assert.That(_f.Catalog.FileSchema("duckdb", Db, Options("table", "levels")).ToString(), Is.EqualTo("id:Integer?, name:Text?, built:Date?"));
        Assert.That(_f.Catalog.FileSchema("duckdb", Db, Options("sql", "SELECT name FROM levels")).ToString(), Is.EqualTo("name:Text?"));
    }

    [Test]
    public void DuckDbFile_without_table_or_query_with_unknown_table_or_missing_file_fails()
    {
        Assert.That(_f.Catalog.FileSchema("duckdb", Db, new Dictionary<string, string>()).Ok, Is.False);
        Assert.That(_f.Catalog.FileSchema("duckdb", Db, Options("table", "nope")).Ok, Is.False);
        Assert.That(_f.Catalog.FileSchema("duckdb", Absent, Options("table", "levels")).Ok, Is.False);
    }

    [Test]
    public void A_format_it_does_not_read_is_a_failed_result_naming_it()
        => Assert.That(_f.Catalog.FileSchema("xlsx", "a.xlsx", Options("sheet", "S")).ToString(), Does.Contain("xlsx"));
}
