namespace BimOpenFlow.Relations.DuckDb.Tests;

[TestFixture]
public sealed class DuckDbCatalogTests
{
    private Fixture _f = null!;
    [OneTimeSetUp] public void Up() => _f = new Fixture();
    [OneTimeTearDown] public void Down() => _f.Dispose();

    [Test]
    public void CsvHeaderIsSniffed()
        => Assert.That(_f.Catalog.CsvSchema("files", "walls.csv").ToString(), Is.EqualTo("id:Integer?, height:Number?, level_id:Integer?, name:Text?"));

    [Test]
    public void TableComesFromInformationSchema()
        => Assert.That(_f.Catalog.TableSchema("db", "levels").ToString(), Is.EqualTo("id:Integer?, name:Text?, built:Date?"));

    [Test]
    public void MissingFileIsAnError()
        => Assert.That(_f.Catalog.CsvSchema("files", "nope.csv").Ok, Is.False);

    [Test]
    public void EscapingTheRootIsAnError()
        => Assert.That(_f.Catalog.CsvSchema("files", "../walls.csv").ToString(), Does.Contain("outside the source root"));

    [Test]
    public void UnknownSourceIsAnError()
        => Assert.That(_f.Catalog.TableSchema("elsewhere", "levels").ToString(), Is.EqualTo("Unknown source 'elsewhere'."));

    [Test]
    public void RawSqlIsTypedByDescribe()
    {
        var walls = _f.Catalog.CsvSchema("files", "walls.csv").Require();
        var result = _f.Catalog.QuerySchema("SELECT id, height * 2 AS doubled, name || '!' AS shout FROM t1", [walls]);
        Assert.That(result.ToString(), Is.EqualTo("id:Integer?, doubled:Number?, shout:Text?"));
    }

    [Test]
    public void WholePlanInfersThroughTheCatalog()
        => Assert.That(Fixture.Walls.Join(Fixture.Levels, "level_id", "id").Infer(_f.Catalog).ToString(),
            Is.EqualTo("id:Integer?, height:Number?, level_id:Integer?, name:Text?, id_right:Integer?, name_right:Text?, built:Date?"));
}
