using Ara3D.NodeGraph;
using BimOpenFlow.Host.Store;

namespace BimOpenFlow.Host.Tests;

/// <summary>TKT-102: restarting the host picks up a changed sample unless the user edited
/// their copy, and never overwrites an edited copy silently. Each test seeds a temp store
/// from a temp sample folder named "demo-analyses".</summary>
[TestFixture]
public sealed class SampleSeedingRefreshTests
{
    private string _root = null!;
    private string _samples = null!;
    private AnalysisStore _store = null!;

    [SetUp]
    public void SetUp()
    {
        _root = Path.Combine(Path.GetTempPath(), "bof-seed-refresh-" + Guid.NewGuid().ToString("N"));
        _samples = Path.Combine(_root, "demo-analyses");
        Directory.CreateDirectory(_samples);
        _store = new AnalysisStore(Path.Combine(_root, "store"));
    }

    [TearDown]
    public void TearDown()
    {
        try { Directory.Delete(_root, recursive: true); }
        catch (IOException) { }
    }

    private static GraphDocument Sample(string file)
        => GraphDocument.Empty.AddNode("read", "csv.read", 1).SetParam("read", "path", "{SAMPLES}/" + file);

    private void WriteSample(string id, GraphDocument doc)
        => File.WriteAllText(Path.Combine(_samples, id + ".json"), doc.ToCanonicalJson());

    private (IReadOnlyList<string> Seeded, string Log) Seed(string target = "tables")
    {
        var log = new StringWriter();
        var seeded = SampleSeeding.Seed(_store, [(_samples, SampleSeeding.PathPlaceholder, Path.Combine(_root, target))],
            registry: null, log);
        return (seeded, log.ToString());
    }

    private string ReadPath(string id)
        => _store.Load(id).Values["read"]["path"];

    [Test]
    public void FreshStore_SeedsAndRecordsTheHashOfTheRewrittenGraph()
    {
        WriteSample("walls", Sample("walls.csv"));
        var (seeded, log) = Seed();
        var record = SampleSeedRecord.Read(_store);
        Assert.Multiple(() =>
        {
            Assert.That(seeded, Is.EqualTo(new[] { "walls" }));
            Assert.That(log, Is.Empty);
            Assert.That(record["walls"].Source, Is.EqualTo("demo-analyses"));
            Assert.That(record["walls"].GraphSha256, Is.EqualTo(SampleSeedRecord.Hash(_store.Load("walls"))));
        });
    }

    [Test]
    public void UnchangedSample_ReseedsNothingAndLogsNothing()
    {
        WriteSample("walls", Sample("walls.csv"));
        Seed();
        var (seeded, log) = Seed();
        Assert.Multiple(() =>
        {
            Assert.That(seeded, Is.Empty);
            Assert.That(log, Is.Empty);
            Assert.That(_store.History("walls"), Is.Empty);
        });
    }

    [Test]
    public void ChangedSample_RefreshesAnUntouchedCopy_AndArchivesTheOld()
    {
        WriteSample("walls", Sample("walls.csv"));
        Seed();
        WriteSample("walls", Sample("walls-v2.csv"));
        var (seeded, log) = Seed();
        Assert.Multiple(() =>
        {
            Assert.That(seeded, Is.Empty);
            Assert.That(log, Does.Contain("refreshed sample analysis walls"));
            Assert.That(ReadPath("walls"), Does.EndWith("/walls-v2.csv"));
            Assert.That(_store.History("walls"), Has.Count.EqualTo(1));
            Assert.That(SampleSeedRecord.Read(_store)["walls"].GraphSha256, Is.EqualTo(SampleSeedRecord.Hash(_store.Load("walls"))));
        });
    }

    /// <summary>The sample a later revision of Sample becomes: a second node wired after the reader.</summary>
    private static GraphDocument SampleWithHead(string file)
        => Sample(file).AddNode("head", "table.head", 1).Connect("read.table", "head.table")
            .SetLayout("read", new NodeLayout(0, 0)).SetLayout("head", new NodeLayout(300, 0));

    private void Drag(string id, string nodeId, NodeLayout to)
        => _store.Save(id, _store.Load(id).SetLayout(nodeId, to));

    [Test]
    public void ChangedSample_RefreshesACopyTheUserOnlyMoved_KeepingTheirPositions()
    {
        // TKT-257: a drag saved the copy; the copy must still count as the untouched sample.
        WriteSample("walls", Sample("walls.csv").SetLayout("read", new NodeLayout(0, 0)));
        Seed();
        Drag("walls", "read", new NodeLayout(619.89, 40, 220, 120));
        Assert.That(Seed().Log, Is.Empty, "a moved node alone is no change to log");
        WriteSample("walls", SampleWithHead("walls-v2.csv"));
        var (_, log) = Seed();
        var copy = _store.Load("walls");
        Assert.Multiple(() =>
        {
            Assert.That(log, Does.Contain("refreshed sample analysis walls"));
            Assert.That(copy.Nodes.Select(n => n.Id), Is.EquivalentTo(new[] { "read", "head" }));
            Assert.That(copy.Edges, Has.Count.EqualTo(1));
            Assert.That(ReadPath("walls"), Does.EndWith("/walls-v2.csv"));
            Assert.That(copy.Layout["read"], Is.EqualTo(new NodeLayout(619.89, 40, 220, 120)));
            Assert.That(copy.Layout["head"], Is.EqualTo(new NodeLayout(300, 0)));
            Assert.That(SampleSeedRecord.Read(_store)["walls"].GraphSha256, Is.EqualTo(SampleSeedRecord.Hash(copy)));
        });
    }

    [Test]
    public void RefreshedLayout_DropsPositionsOfNodesTheSampleRemoved()
    {
        WriteSample("walls", SampleWithHead("walls.csv"));
        Seed();
        Drag("walls", "head", new NodeLayout(900, 900));
        WriteSample("walls", Sample("walls-v2.csv").SetLayout("read", new NodeLayout(0, 0)));
        Seed();
        Assert.That(_store.Load("walls").Layout.Keys, Is.EquivalentTo(new[] { "read" }));
    }

    /// <summary>Rewrites the record as a host before 2026-10-11 wrote it: the whole document's hash.</summary>
    private void WriteLegacyRecord(string id, GraphDocument seeded)
        => File.WriteAllText(SampleSeedRecord.PathOf(_store),
            $$"""{ "{{id}}": { "source": "demo-analyses", "sha256": "{{SampleSeedRecord.WholeDocumentHash(seeded)}}" } }""");

    [Test]
    public void LegacyRecord_RefreshesACopyTheUserOnlyMoved()
    {
        // The stale bfast-buffers copy: seeded under the whole-document hash, then one node dragged.
        WriteSample("walls", Sample("walls.csv").SetLayout("read", new NodeLayout(0, 0)));
        Seed();
        WriteLegacyRecord("walls", _store.Load("walls"));
        Drag("walls", "read", new NodeLayout(619.89, 0));
        WriteSample("walls", SampleWithHead("walls-v2.csv"));
        var (_, log) = Seed();
        var entry = SampleSeedRecord.Read(_store)["walls"];
        Assert.Multiple(() =>
        {
            Assert.That(log, Does.Contain("refreshed sample analysis walls"));
            Assert.That(ReadPath("walls"), Does.EndWith("/walls-v2.csv"));
            Assert.That(_store.Load("walls").Layout["read"], Is.EqualTo(new NodeLayout(619.89, 0)));
            Assert.That(entry.IsLegacy, Is.False);
            Assert.That(entry.Sha256, Is.Null);
            Assert.That(File.ReadAllText(SampleSeedRecord.PathOf(_store)), Does.Not.Contain("\"sha256\""));
        });
    }

    [Test]
    public void UnrecordedEntry_RefreshesACopyTheUserOnlyMoved_AndKeepsOneWithAnEditedValue()
    {
        // A store older than the record writes an empty hash for a copy with saved versions;
        // its oldest saved document is the seed. walls was only moved; doors had a value changed.
        foreach (var id in new[] { "walls", "doors" })
        {
            WriteSample(id, Sample(id + ".csv").SetLayout("read", new NodeLayout(0, 0)));
            _store.Save(id, SampleSeeding.RewritePaths(Sample(id + ".csv").SetLayout("read", new NodeLayout(0, 0)), Path.Combine(_root, "tables")));
        }
        Drag("walls", "read", new NodeLayout(619.89, 0));
        _store.Save("doors", _store.Load("doors").SetParam("read", "type", "int16"));
        Drag("doors", "read", new NodeLayout(619.89, 0));
        File.WriteAllText(SampleSeedRecord.PathOf(_store),
            """{ "walls": { "source": "demo-analyses", "sha256": "" }, "doors": { "source": "demo-analyses", "sha256": "" } }""");
        WriteSample("walls", SampleWithHead("walls-v2.csv"));
        WriteSample("doors", Sample("doors-v2.csv"));
        var (_, log) = Seed();
        Assert.Multiple(() =>
        {
            Assert.That(log, Does.Contain("refreshed sample analysis walls"));
            Assert.That(_store.Load("walls").Layout["read"], Is.EqualTo(new NodeLayout(619.89, 0)));
            Assert.That(log, Does.Contain("kept your edited copy of sample analysis doors"));
            Assert.That(_store.Load("doors").Values["read"]["type"], Is.EqualTo("int16"));
        });
    }

    [Test]
    public void LegacyRecord_KeepsACopyTheUserEdited()
    {
        WriteSample("walls", Sample("walls.csv"));
        Seed();
        WriteLegacyRecord("walls", _store.Load("walls"));
        _store.Save("walls", _store.Load("walls").SetParam("read", "path", "C:/mine.csv"));
        WriteSample("walls", Sample("walls-v2.csv"));
        Assert.That(Seed().Log, Does.Contain("kept your edited copy of sample analysis walls"));
        Assert.That(ReadPath("walls"), Is.EqualTo("C:/mine.csv"));
    }

    [Test]
    public void LegacyRecord_LeavesADeletedCopyDeletedUntilTheSampleChanges()
    {
        WriteSample("walls", Sample("walls.csv"));
        Seed();
        WriteLegacyRecord("walls", _store.Load("walls"));
        _store.Delete("walls");
        Assert.That(Seed().Seeded, Is.Empty);
        WriteSample("walls", Sample("walls-v2.csv"));
        Assert.That(Seed().Seeded, Is.EqualTo(new[] { "walls" }));
    }

    [Test]
    public void ChangedSample_KeepsAnEditedCopy_AndSaysSo()
    {
        WriteSample("walls", Sample("walls.csv"));
        Seed();
        _store.Save("walls", _store.Load("walls").SetParam("read", "path", "C:/mine.csv"));
        WriteSample("walls", Sample("walls-v2.csv"));
        var (_, log) = Seed();
        Assert.Multiple(() =>
        {
            Assert.That(ReadPath("walls"), Is.EqualTo("C:/mine.csv"));
            Assert.That(log, Does.Contain("kept your edited copy of sample analysis walls"));
            Assert.That(log, Does.Contain("samples/demo-analyses/walls.json"));
        });
        // Asked again, it keeps the copy again rather than taking the sample on the second try.
        Assert.That(Seed().Log, Does.Contain("kept your edited copy"));
        Assert.That(ReadPath("walls"), Is.EqualTo("C:/mine.csv"));
    }

    [Test]
    public void PlaceholderTarget_IsPartOfTheHash_SoAMovedTargetRefreshesOnlyUntouchedCopies()
    {
        // The DuckDB studio's case: the sample is unchanged but the machine-local path it is
        // rewritten to differs. A byte compare of store copy and sample file always differs;
        // the recorded hash is of the rewritten graph, so an unchanged target is no change.
        WriteSample("walls", Sample("walls.csv"));
        WriteSample("doors", Sample("doors.csv"));
        Seed("old-target");
        Assert.That(Seed("old-target").Log, Is.Empty);
        _store.Save("doors", _store.Load("doors").SetParam("read", "path", "C:/mine.csv"));
        var (_, log) = Seed("new-target");
        Assert.Multiple(() =>
        {
            Assert.That(ReadPath("walls"), Does.Contain("/new-target/"));
            Assert.That(ReadPath("doors"), Is.EqualTo("C:/mine.csv"));
            Assert.That(log, Does.Contain("kept your edited copy of sample analysis doors"));
        });
    }

    [Test]
    public void DeletedCopy_StaysDeletedUntilTheSampleChanges()
    {
        WriteSample("walls", Sample("walls.csv"));
        Seed();
        _store.Delete("walls");
        Assert.That(Seed().Seeded, Is.Empty);
        Assert.That(_store.Exists("walls"), Is.False);
        WriteSample("walls", Sample("walls-v2.csv"));
        Assert.That(Seed().Seeded, Is.EqualTo(new[] { "walls" }));
    }

    [Test]
    public void NewSampleInAFollowedFolder_IsSeeded()
    {
        WriteSample("walls", Sample("walls.csv"));
        Seed();
        WriteSample("doors", Sample("doors.csv"));
        Assert.That(Seed().Seeded, Is.EqualTo(new[] { "doors" }));
    }

    [Test]
    public void LegacyStore_RefreshesCopiesWithoutVersions_KeepsEditedOnes_AndLeavesTrashAlone()
    {
        // A store seeded before .samples.json existed: three copies of an older sample, one
        // edited (it has a saved version), one deleted.
        foreach (var id in new[] { "walls", "doors", "slabs" })
        {
            WriteSample(id, Sample(id + ".csv"));
            _store.Save(id, SampleSeeding.RewritePaths(Sample(id + ".csv"), Path.Combine(_root, "tables")));
        }
        _store.Save("doors", _store.Load("doors").SetParam("read", "path", "C:/mine.csv"));
        _store.Delete("slabs");
        foreach (var id in new[] { "walls", "doors", "slabs" })
            WriteSample(id, Sample(id + "-v2.csv"));

        var (seeded, log) = Seed();
        Assert.Multiple(() =>
        {
            Assert.That(seeded, Is.Empty);
            Assert.That(ReadPath("walls"), Does.EndWith("/walls-v2.csv"));
            Assert.That(ReadPath("doors"), Is.EqualTo("C:/mine.csv"));
            Assert.That(log, Does.Contain("kept your edited copy of sample analysis doors"));
            Assert.That(_store.Exists("slabs"), Is.False);
        });

        // The kept copy's record holds no hash, so deleting it takes the new sample next time.
        _store.Delete("doors");
        Assert.That(Seed().Seeded, Is.EqualTo(new[] { "doors" }));
        Assert.That(ReadPath("doors"), Does.EndWith("/doors-v2.csv"));
    }

    [Test]
    public void StoreHoldingNothingFromAFolder_DoesNotFollowIt()
    {
        _store.Create("mine");
        WriteSample("walls", Sample("walls.csv"));
        var (seeded, log) = Seed();
        Assert.Multiple(() =>
        {
            Assert.That(seeded, Is.Empty);
            Assert.That(log, Is.Empty);
            Assert.That(File.Exists(SampleSeedRecord.PathOf(_store)), Is.False);
        });
    }
}
