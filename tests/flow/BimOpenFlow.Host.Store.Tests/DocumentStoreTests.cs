using Ara3D.NodeGraph;

namespace BimOpenFlow.Host.Store.Tests;

[TestFixture]
public class DocumentStoreTests : StoreFixture
{
    [Test]
    public void SaveLoad_RoundTrip_ByteIdentity()
    {
        var doc = StoreTestData.Doc();
        Assert.That(Store.Save("tower-a", doc), Is.True);
        var loaded = Store.Load("tower-a");
        Assert.That(loaded.ToCanonicalJson(), Is.EqualTo(doc.ToCanonicalJson()));
        var bytes = File.ReadAllText(Path.Combine(RootDir, "tower-a", AnalysisStore.CurrentFileName),
            GraphDocumentIO.Utf8NoBom);
        Assert.That(bytes, Is.EqualTo(doc.ToCanonicalJson()));
    }

    [Test]
    public void Save_Unchanged_NoOps()
    {
        var doc = StoreTestData.Doc();
        Store.Save("a1", doc);
        var written = File.GetLastWriteTimeUtc(Path.Combine(RootDir, "a1", AnalysisStore.CurrentFileName));
        Assert.That(Store.Save("a1", doc), Is.False);
        Assert.Multiple(() =>
        {
            Assert.That(Store.History("a1"), Is.Empty, "no version archived on a no-op save");
            Assert.That(File.GetLastWriteTimeUtc(Path.Combine(RootDir, "a1", AnalysisStore.CurrentFileName)),
                Is.EqualTo(written), "current not rewritten on a no-op save");
        });
    }

    [Test]
    public void Save_ArchivesPreviousVersions_InSequence()
    {
        var v1 = StoreTestData.Doc("one.bos");
        var v2 = StoreTestData.Doc("two.bos");
        var v3 = StoreTestData.Doc("three.bos");
        Store.Save("a1", v1);
        Store.Save("a1", v2);
        Store.Save("a1", v3);

        var history = Store.History("a1");
        Assert.That(history.Select(v => v.Sequence), Is.EqualTo(new[] { 1, 2 }));
        Assert.Multiple(() =>
        {
            Assert.That(history[0].FileName, Is.EqualTo("0001.dfg.json"));
            Assert.That(history[0].GraphHash, Is.EqualTo(v1.ComputeGraphHash()));
            Assert.That(history[1].GraphHash, Is.EqualTo(v2.ComputeGraphHash()));
            Assert.That(Store.LoadVersion("a1", 1).ToCanonicalJson(), Is.EqualTo(v1.ToCanonicalJson()));
            Assert.That(Store.LoadVersion("a1", 2).ToCanonicalJson(), Is.EqualTo(v2.ToCanonicalJson()));
            Assert.That(Store.Load("a1").ToCanonicalJson(), Is.EqualTo(v3.ToCanonicalJson()));
        });
    }

    [Test]
    public void List_ReturnsSortedEntries_WithSidecarName()
    {
        Store.Save("beta", StoreTestData.Doc());
        Store.Save("alpha", StoreTestData.Doc());
        File.WriteAllText(Path.Combine(RootDir, "alpha", AnalysisStore.NameFileName), "Tower A Clash Check\n");

        var entries = Store.List();
        Assert.That(entries.Select(e => e.Id), Is.EqualTo(new[] { "alpha", "beta" }));
        Assert.Multiple(() =>
        {
            Assert.That(entries[0].Name, Is.EqualTo("Tower A Clash Check"));
            Assert.That(entries[1].Name, Is.EqualTo("beta"), "name falls back to the id");
        });
    }

    [Test]
    public void Delete_MovesToTrash_AndListExcludesIt()
    {
        Store.Save("doomed", StoreTestData.Doc());
        Store.Save("kept", StoreTestData.Doc());
        Store.Delete("doomed");

        Assert.Multiple(() =>
        {
            Assert.That(Store.List().Select(e => e.Id), Is.EqualTo(new[] { "kept" }));
            Assert.That(File.Exists(Path.Combine(RootDir, AnalysisStore.TrashDirName, "doomed",
                AnalysisStore.CurrentFileName)), Is.True, "trashed copy keeps its contents");
            Assert.That(Directory.Exists(Path.Combine(RootDir, "doomed")), Is.False);
        });
    }

    [Test]
    public void Delete_SameIdTwice_KeepsBothInTrash()
    {
        Store.Save("dup", StoreTestData.Doc("one.bos"));
        Store.Delete("dup");
        Store.Save("dup", StoreTestData.Doc("two.bos"));
        Store.Delete("dup");

        var trash = Path.Combine(RootDir, AnalysisStore.TrashDirName);
        Assert.That(Directory.Exists(Path.Combine(trash, "dup")), Is.True);
        Assert.That(Directory.Exists(Path.Combine(trash, "dup-2")), Is.True);
    }

    [TestCase("Tower")]
    [TestCase("a.b")]
    [TestCase("a/b")]
    [TestCase("a\\b")]
    [TestCase("")]
    [TestCase("-leading")]
    [TestCase("trailing-")]
    [TestCase("..")]
    [TestCase("has space")]
    public void InvalidIds_Rejected(string id)
        => Assert.Throws<ArgumentException>(() => Store.Save(id, StoreTestData.Doc()));

    [Test]
    public void Create_MakesEmptyAnalysis_AndRefusesDuplicates()
    {
        Store.Create("fresh");
        Assert.That(Store.Load("fresh"), Is.EqualTo(GraphDocument.Empty));
        Assert.Throws<InvalidOperationException>(() => Store.Create("fresh"));
    }

    [Test]
    public void Load_MissingAnalysis_Throws()
        => Assert.That(() => Store.Load("nope"), Throws.InstanceOf<IOException>());
}
