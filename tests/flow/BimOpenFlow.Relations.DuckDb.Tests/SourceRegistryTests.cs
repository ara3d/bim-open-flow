using BimOpenFlow.Relations.DuckDb;

namespace BimOpenFlow.Relations.DuckDb.Tests;

/// <summary>Source naming from roots: the snapshot registry, the live rescanning registry that
/// finds a database written after construction, and the wrapper that names a source still
/// being prepared.</summary>
[TestFixture]
public sealed class SourceRegistryTests
{
    private string _root = null!;

    [SetUp]
    public void NewRoot()
    {
        _root = Path.Combine(Path.GetTempPath(), "bof-registry-tests", Guid.NewGuid().ToString("N"), "models");
        Directory.CreateDirectory(_root);
    }

    [TearDown]
    public void DeleteRoot()
    {
        try { Directory.Delete(Path.GetDirectoryName(_root)!, recursive: true); }
        catch (IOException) { }
    }

    private void Touch(string fileName)
        => File.WriteAllBytes(Path.Combine(_root, fileName), []);

    [Test]
    public void Snapshot_NamesTheFolderAndEachDatabase()
    {
        Touch("a.duckdb");
        var registry = SourceRegistries.FromRoots([_root]);
        Assert.Multiple(() =>
        {
            Assert.That(registry.Resolve("models"), Is.EqualTo(new SourceLocation(SourceType.FileRoot, _root)));
            Assert.That(registry.Resolve("a"), Is.EqualTo(new SourceLocation(SourceType.DuckDbFile, Path.Combine(_root, "a.duckdb"))));
            Assert.That(registry.Resolve("b"), Is.Null);
        });
    }

    [Test]
    public void Snapshot_DoesNotSeeADatabaseWrittenLater()
    {
        var registry = SourceRegistries.FromRoots([_root]);
        Touch("later.duckdb");
        Assert.That(registry.Resolve("later"), Is.Null);
    }

    [Test]
    public void RootScan_SeesADatabaseWrittenLater()
    {
        var registry = new RootScanRegistry([_root]);
        Assert.That(registry.Resolve("later"), Is.Null);
        Touch("later.duckdb");
        Assert.That(registry.Resolve("later")?.Type, Is.EqualTo(SourceType.DuckDbFile));
        Assert.That(registry.Names, Does.Contain("later"));
    }

    [Test]
    public void RootScan_SkipsRootsThatDoNotExistYet()
    {
        var missing = Path.Combine(_root, "not-yet");
        var registry = new RootScanRegistry([missing]);
        Assert.That(registry.Resolve("not-yet"), Is.Null);
        Directory.CreateDirectory(missing);
        Assert.That(registry.Resolve("not-yet")?.Type, Is.EqualTo(SourceType.FileRoot));
    }

    [Test]
    public void Preparing_ResolvesWhatTheInnerRegistryHas()
    {
        Touch("a.duckdb");
        var registry = new PreparingRegistry(new RootScanRegistry([_root]), _ => "never asked");
        Assert.That(registry.Resolve("a")?.Type, Is.EqualTo(SourceType.DuckDbFile));
    }

    [Test]
    public void Preparing_NamesTheSourceAndTheReasonWhileItIsMissing()
    {
        var registry = new PreparingRegistry(new RootScanRegistry([_root]),
            name => name == "a" ? "building a.duckdb from a.ifc" : null);
        var error = Assert.Throws<SourcePreparingException>(() => registry.Resolve("a"))!;
        Assert.Multiple(() =>
        {
            Assert.That(error.Source, Is.EqualTo("a"));
            Assert.That(error.Message, Is.EqualTo("Source 'a' is not ready yet: building a.duckdb from a.ifc"));
            Assert.That(registry.Resolve("b"), Is.Null, "a name nobody is preparing stays unknown");
        });
    }

    [Test]
    public void Preparing_StopsThrowingOnceTheFileArrives()
    {
        var registry = new PreparingRegistry(new RootScanRegistry([_root]), name => name == "a" ? "building" : null);
        Assert.Throws<SourcePreparingException>(() => registry.Resolve("a"));
        Touch("a.duckdb");
        Assert.That(registry.Resolve("a")?.Type, Is.EqualTo(SourceType.DuckDbFile));
    }

    [Test]
    public void Require_ReportsAPreparingSourceInTheSchemaError()
    {
        var registry = new PreparingRegistry(new RootScanRegistry([_root]), name => name == "db" ? "building" : null);
        var schema = new DuckDbCatalog(registry).TableSchema("db", "EntityText");
        Assert.Multiple(() =>
        {
            Assert.That(schema.Ok, Is.False);
            Assert.That(schema.ToString(), Does.Contain("Source 'db' is not ready yet: building"));
        });
    }
}
