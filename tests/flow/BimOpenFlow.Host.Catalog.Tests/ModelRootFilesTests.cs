using static BimOpenFlow.Host.Catalog.Tests.CatalogTestHelpers;

namespace BimOpenFlow.Host.Catalog.Tests;

[TestFixture]
public sealed class ModelRootFilesTests
{
    private string _root = null!;
    private string _other = null!;
    private ModelRootFiles _files = null!;

    [SetUp]
    public void SetUp()
    {
        _root = NewTempDir();
        _other = NewTempDir();
        _files = new ModelRootFiles([_root]);
    }

    [TearDown]
    public void TearDown()
    {
        DeleteTempDir(_root);
        DeleteTempDir(_other);
    }

    private static string Slash(string path) => path.Replace('\\', '/');

    [Test]
    public void List_FiltersByExtensionAndReportsRootRelativePaths()
    {
        WriteFile(_root, "a.bos");
        WriteFile(_root, "sub/b.BOS");
        WriteFile(_root, "sub/c.ifc");
        WriteFile(_root, "notes.txt");
        var found = _files.List(".bos, ifc", out var truncated);
        Assert.That(truncated, Is.False);
        Assert.That(found.Select(f => f.Relative), Is.EqualTo(new[] { "a.bos", "sub/b.BOS", "sub/c.ifc" }));
        Assert.That(found[0].Path, Is.EqualTo(Slash(Path.GetFullPath(Path.Combine(_root, "a.bos")))));
        Assert.That(found[0].SizeBytes, Is.EqualTo(4));
    }

    [Test]
    public void List_EmptyExtensionsAcceptsEveryFile_AndAMissingRootContributesNothing()
    {
        WriteFile(_root, "a.bos");
        WriteFile(_root, "notes.txt");
        var files = new ModelRootFiles([_root, Path.Combine(_root, "missing")]);
        Assert.That(files.List("", out _).Count, Is.EqualTo(2));
    }

    [Test]
    public void Browse_EmptyDirListsTheRoots()
    {
        var folder = _files.Browse("", ".bos");
        Assert.That(folder.Parent, Is.Null);
        Assert.That(folder.Entries.Select(e => e.Path), Is.EqualTo(_files.Roots));
        Assert.That(folder.Entries.All(e => e.IsDirectory));
    }

    [Test]
    public void Browse_ListsFoldersThenMatchingFiles_AndAParentThatEndsAtTheRoots()
    {
        WriteFile(_root, "z.bos");
        WriteFile(_root, "skip.txt");
        WriteFile(_root, "sub/inner.bos");
        var top = _files.Browse(_root, ".bos");
        Assert.That(top.Entries.Select(e => (e.Name, e.IsDirectory)),
            Is.EqualTo(new[] { ("sub", true), ("z.bos", false) }));
        Assert.That(top.Parent, Is.EqualTo(""));
        var sub = _files.Browse(top.Entries[0].Path, ".bos");
        Assert.That(sub.Entries.Select(e => e.Name), Is.EqualTo(new[] { "inner.bos" }));
        Assert.That(sub.Parent, Is.EqualTo(top.Dir));
    }

    [Test]
    public void Browse_RefusesFoldersOutsideTheRoots_AndDotDotEscapes()
    {
        Assert.Throws<ArgumentException>(() => _files.Browse(_other, ""));
        Assert.Throws<ArgumentException>(() => _files.Browse(Path.Combine(_root, "..", Path.GetFileName(_other)), ""));
        Assert.Throws<DirectoryNotFoundException>(() => _files.Browse(Path.Combine(_root, "nope"), ""));
    }

    [Test]
    public void Stat_FindsFilesAndGlobs_AndNamesTheRootOnlyForPathsUnderIt()
    {
        var inside = WriteFile(_root, "data/a.csv");
        var outside = WriteFile(_other, "b.csv");
        var status = _files.Stat(inside);
        Assert.That(status.Exists, Is.True);
        Assert.That(status.Root, Is.EqualTo(_files.Roots[0]));
        Assert.That(status.Relative, Is.EqualTo("data/a.csv"));
        var elsewhere = _files.Stat(outside);
        Assert.That(elsewhere.Exists, Is.True);
        Assert.That(elsewhere.Root, Is.Null);
        Assert.That(_files.Stat(Path.Combine(_root, "data", "*.csv")).Exists, Is.True);
        Assert.That(_files.Stat(Path.Combine(_root, "data", "*.xlsx")).Exists, Is.False);
        Assert.That(_files.Stat(Path.Combine(_root, "gone.bos")).Exists, Is.False);
        Assert.That(_files.Stat("").Exists, Is.False);
    }
}
