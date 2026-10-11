using System.Diagnostics;
using Ara3D.BimOpenSchema.IO;
using Ara3D.Utils;
using static BimOpenFlow.Host.Catalog.Tests.CatalogTestHelpers;

namespace BimOpenFlow.Host.Catalog.Tests;

/// <summary>GetInfo and GetEntityIndex read only the tables they use (ModelInfo.TablesRead,
/// ModelEntityIndex.TablesRead). A table left unread comes back empty, so a wrong choice would give a
/// wrong count or a missing parameter without an error: each model is also read in full here and the
/// two answers are compared.</summary>
[TestFixture]
public sealed class SelectiveReadTests
{
    private string _root = "";
    private string _cache = "";

    [SetUp]
    public void Create()
    {
        _root = NewTempDir();
        _cache = NewTempDir();
    }

    [TearDown]
    public void Cleanup()
    {
        DeleteTempDir(_root);
        DeleteTempDir(_cache);
    }

    private (ModelCatalog Catalog, ModelEntry Entry) CatalogOf(string bos)
    {
        File.Copy(bos, Path.Combine(_root, Path.GetFileName(bos)));
        var catalog = new ModelCatalog(_root, _cache);
        return (catalog, catalog.Scan().Single());
    }

    private static long ReadMilliseconds(string bos, BosTables tables)
    {
        var best = long.MaxValue;
        for (var i = 0; i < 3; i++)
        {
            var watch = Stopwatch.StartNew();
            new FilePath(bos).ReadBimDataFromParquetZip(tables);
            best = Math.Min(best, watch.ElapsedMilliseconds);
        }
        return best;
    }

    private void AssertSameAsFullRead(string bos)
    {
        var (catalog, entry) = CatalogOf(bos);
        var full = new FilePath(bos).ReadBimDataFromParquetZip();

        var watch = Stopwatch.StartNew();
        var info = catalog.GetInfo(entry);
        var infoMs = watch.ElapsedMilliseconds;
        watch.Restart();
        var index = catalog.GetEntityIndex(entry);
        var indexMs = watch.ElapsedMilliseconds;
        var expected = ModelEntityIndex.Build(full);
        TestContext.Progress.WriteLine(
            $"{Path.GetFileName(bos)} ({expected.Count} entities): GetInfo {infoMs} ms, GetEntityIndex {indexMs} ms; " +
            $"read alone, best of 3: {ReadMilliseconds(bos, ModelInfo.TablesRead)} ms for info, " +
            $"{ReadMilliseconds(bos, ModelEntityIndex.TablesRead)} ms for the index, {ReadMilliseconds(bos, BosTables.All)} ms in full");

        Assert.That(info, Is.EqualTo(new ModelInfo(full.Entities.Length, full.Parameters.Length, full.Documents.Length, full.Relations.Length)));
        Assert.That(info.EntityCount, Is.GreaterThan(0));
        Assert.That(index.Count, Is.EqualTo(expected.Count));
        var actualAll = index.All.ToList();
        var expectedAll = expected.All.ToList();
        for (var i = 0; i < expectedAll.Count; i++)
        {
            var (a, e) = (actualAll[i], expectedAll[i]);
            Assert.That((a.LocalId, a.GlobalId, a.Name, a.Category), Is.EqualTo((e.LocalId, e.GlobalId, e.Name, e.Category)), $"entity {i}");
            Assert.That(a.Parameters, Is.EqualTo(e.Parameters), $"entity {i} ({e.LocalId}) parameters");
        }
        Assert.That(expectedAll.Sum(e => e.Parameters.Count), Is.GreaterThan(0));
    }

    [Test]
    public void Synthetic_model_gives_the_same_info_and_index_as_a_full_read()
    {
        var bos = Path.Combine(NewTempDir(), "sample.bos");
        try
        {
            SampleBos.Write(bos);
            AssertSameAsFullRead(bos);
        }
        finally
        {
            DeleteTempDir(Path.GetDirectoryName(bos)!);
        }
    }

    [TestCase("duplex.bos")]
    [TestCase("schependomlaan.bos")]
    public void Public_sample_gives_the_same_info_and_index_as_a_full_read(string sample)
        => AssertSameAsFullRead(FindPublicSample(sample));
}
