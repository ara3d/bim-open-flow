using Ara3D.BimOpenSchema.DuckDb;
using Ara3D.DataFlowEngine.Abstractions;
using Ara3D.Utils;
using BimOpenFlow.Nodes.Relations;
using BimOpenFlow.Relations;
using BimOpenFlow.Relations.DuckDb;

namespace BimOpenFlow.Host.Tests;

/// <summary>A temp folder holding one DuckDB file ("db") and one CSV under a file root
/// ("files"), registered so SourceFiles can resolve real source reads.</summary>
[TestFixture]
public sealed class RelationHostResultsTests
{
    private string _folder = null!;
    private string _db = null!;
    private RelationHostResults _results = null!;

    [SetUp]
    public void SetUp()
    {
        _folder = Path.Combine(Path.GetTempPath(), "bof-relationhostresults-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_folder);
        File.WriteAllText(Path.Combine(_folder, "walls.csv"), "id,height\n1,2.5\n");
        _db = Path.Combine(_folder, "levels.duckdb");
        using (BosDuckDb.Open(new FilePath(_db))) { }
        var registry = ConnectionRegistry.Of(("files", SourceType.FileRoot, _folder), ("db", SourceType.DuckDbFile, _db));
        _results = new RelationHostResults(new RelationRuntime(registry));
    }

    [TearDown]
    public void TearDown()
    {
        try { Directory.Delete(_folder, recursive: true); }
        catch (IOException) { }
    }

    private static RelationValue Of(Plan plan) => new(plan.Text, plan.Hash, plan);

    [Test]
    public void ReadTable_GivesTheDuckDbFile()
        => Assert.That(_results.SourceFiles(Of(Plans.Table("db", "t"))), Is.EqualTo(new[] { Path.GetFullPath(_db) }));

    [Test]
    public void ReadCsv_GivesTheFileUnderTheRoot()
        => Assert.That(_results.SourceFiles(Of(Plans.Csv("files", "walls.csv"))),
            Is.EqualTo(new[] { Path.Combine(Path.GetFullPath(_folder), "walls.csv") }));

    [Test]
    public void AFilterPlan_GivesNone()
        => Assert.That(_results.SourceFiles(Of(Plans.Table("db", "t").Filter("[id] > 0"))), Is.Empty);

    [Test]
    public void AnUnknownSource_GivesNone()
        => Assert.That(_results.SourceFiles(Of(Plans.Table("nope", "t"))), Is.Empty);
}
