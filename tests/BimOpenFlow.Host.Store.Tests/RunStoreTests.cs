using Ara3D.DataFlowEngine.Runs;

namespace BimOpenFlow.Host.Store.Tests;

[TestFixture]
public class RunStoreTests : StoreFixture
{
    [Test]
    public void SaveRun_RoundTrips_WithExpectedFileName()
    {
        Store.Save("a1", StoreTestData.Doc());
        var run = StoreTestData.Run();
        var fileName = Store.SaveRun("a1", run);
        Assert.That(fileName, Is.EqualTo("20260831T120000123Z-aaaaaaaa.run.json"));
        var loaded = Store.LoadRun("a1", fileName);
        Assert.That(loaded.ToCanonicalJson(), Is.EqualTo(run.ToCanonicalJson()));
    }

    [Test]
    public void SaveRun_SameRunTwice_RefusesOverwrite()
    {
        Store.Save("a1", StoreTestData.Doc());
        var run = StoreTestData.Run();
        Store.SaveRun("a1", run);
        Assert.Throws<IOException>(() => Store.SaveRun("a1", run));
    }

    [Test]
    public void ListRuns_ChronologicalOrder_EmptyWhenNone()
    {
        Store.Save("a1", StoreTestData.Doc());
        Assert.That(Store.ListRuns("a1"), Is.Empty);
        var later = Store.SaveRun("a1", StoreTestData.Run("2026-08-31T13:00:00.000Z"));
        var earlier = Store.SaveRun("a1", StoreTestData.Run("2026-08-31T11:00:00.000Z"));
        Assert.That(Store.ListRuns("a1"), Is.EqualTo(new[] { earlier, later }));
    }
}
