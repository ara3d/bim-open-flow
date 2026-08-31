using static BimOpenFlow.Host.Catalog.Tests.CatalogTestHelpers;

namespace BimOpenFlow.Host.Catalog.Tests;

[TestFixture]
public sealed class ScanTests
{
    private string _root = null!;
    private string _cache = null!;

    [SetUp]
    public void SetUp()
    {
        _root = NewTempDir();
        _cache = NewTempDir();
    }

    [TearDown]
    public void TearDown()
    {
        DeleteTempDir(_root);
        DeleteTempDir(_cache);
    }

    private ModelCatalog Catalog(params string[] roots)
        => new(roots.Length == 0 ? [_root] : roots, _cache, new StubConverter());

    [Test]
    public void Scan_FindsIfcAndBosRecursively_IgnoresOtherFiles()
    {
        WriteFile(_root, "a.ifc");
        WriteFile(_root, Path.Combine("sub", "deep", "b.bos"));
        WriteFile(_root, "notes.txt");
        WriteFile(_root, "c.json");

        var entries = Catalog().Scan();

        Assert.That(entries, Has.Count.EqualTo(2));
        Assert.That(entries.Select(e => e.Kind), Is.EquivalentTo(new[] { ModelKind.Ifc, ModelKind.Bos }));
        Assert.That(entries.Select(e => e.Name), Is.EquivalentTo(new[] { "a", "b" }));
    }

    [Test]
    public void Scan_PopulatesSizeHashAndTimestamp()
    {
        var path = WriteFile(_root, "m.ifc", "some model content");
        var entry = Catalog().Scan().Single();

        Assert.That(entry.SourcePath, Is.EqualTo(path));
        Assert.That(entry.SizeBytes, Is.EqualTo(new FileInfo(path).Length));
        Assert.That(entry.ContentHash, Has.Length.EqualTo(64));
        Assert.That(entry.LastWriteUtc, Is.EqualTo(File.GetLastWriteTimeUtc(path)));
    }

    [Test]
    public void Scan_IdsAreStableAcrossScansAndCatalogInstances()
    {
        WriteFile(_root, Path.Combine("Models", "Duplex A.ifc"));
        WriteFile(_root, "site.bos");

        var first = Catalog().Scan().Select(e => e.Id).ToList();
        var second = Catalog().Scan().Select(e => e.Id).ToList();
        var fresh = new ModelCatalog(_root, _cache, new StubConverter()).Scan().Select(e => e.Id).ToList();

        Assert.That(second, Is.EqualTo(first));
        Assert.That(fresh, Is.EqualTo(first));
        Assert.That(first, Is.EquivalentTo(new[] { "models-duplex-a.ifc", "site.bos" }));
    }

    [Test]
    public void Scan_IdSurvivesContentEdit_ButHashChanges()
    {
        WriteFile(_root, "m.ifc", "version 1");
        var before = Catalog().Scan().Single();

        WriteFile(_root, "m.ifc", "version 2");
        var after = Catalog().Scan().Single();

        Assert.That(after.Id, Is.EqualTo(before.Id));
        Assert.That(after.ContentHash, Is.Not.EqualTo(before.ContentHash));
    }

    [Test]
    public void Scan_CollidingRelativePathsAcrossRoots_GetHashSuffix()
    {
        var root2 = NewTempDir();
        try
        {
            WriteFile(_root, "m.ifc", "content one");
            WriteFile(root2, "m.ifc", "content two");

            var entries = Catalog(_root, root2).Scan();

            Assert.That(entries, Has.Count.EqualTo(2));
            Assert.That(entries[0].Id, Is.EqualTo("m.ifc"));
            Assert.That(entries[1].Id, Is.EqualTo($"m.ifc-{entries[1].ContentHash[..8]}"));
        }
        finally
        {
            DeleteTempDir(root2);
        }
    }

    [Test]
    public void Scan_MissingRootContributesNothing()
    {
        WriteFile(_root, "m.bos");
        var entries = Catalog(_root, Path.Combine(_root, "does-not-exist")).Scan();
        Assert.That(entries, Has.Count.EqualTo(1));
    }

    [Test]
    public void Scan_ReflectsAddedAndRemovedFiles()
    {
        var catalog = Catalog();
        Assert.That(catalog.Scan(), Is.Empty);

        var path = WriteFile(_root, "m.bos");
        Assert.That(catalog.Scan(), Has.Count.EqualTo(1));

        File.Delete(path);
        Assert.That(catalog.Scan(), Is.Empty);
    }
}
