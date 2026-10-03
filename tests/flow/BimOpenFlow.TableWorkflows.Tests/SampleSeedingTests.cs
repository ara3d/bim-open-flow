using BimOpenFlow.Host;
using BimOpenFlow.Host.Store;

namespace BimOpenFlow.TableWorkflows.Tests;

/// <summary>Seeding the sample analyses into an analysis store: an empty store
/// gets them all with {SAMPLES} rewritten; a store holding none of them is left alone.</summary>
[TestFixture]
public sealed class SampleSeedingTests
{
    private string _storeDir = null!;

    [SetUp]
    public void NewStoreDir()
    {
        _storeDir = Path.Combine(Path.GetTempPath(), "bimopenflow-seeding-tests", Guid.NewGuid().ToString("N"));
    }

    [TearDown]
    public void DeleteStoreDir()
    {
        try
        {
            Directory.Delete(_storeDir, recursive: true);
        }
        catch (IOException)
        {
        }
    }

    private static IReadOnlyList<string> ExpectedIds
        => Directory.EnumerateFiles(SamplePaths.AnalysesDir, "*.json")
            .Select(Path.GetFileNameWithoutExtension)
            .Order(StringComparer.Ordinal)
            .ToList()!;

    [Test]
    public void EmptyStore_SeedsAllSamples()
    {
        var store = new AnalysisStore(_storeDir);
        var seeded = SampleSeeding.Seed(store, SamplePaths.AnalysesDir, SamplePaths.TablesDir);
        Assert.That(seeded, Is.EqualTo(ExpectedIds));
        Assert.That(store.List().Select(e => e.Id), Is.EqualTo(ExpectedIds));
    }

    [Test]
    public void Seeding_RewritesThePathPlaceholder()
    {
        var store = new AnalysisStore(_storeDir);
        SampleSeeding.Seed(store, SamplePaths.AnalysesDir, SamplePaths.TablesDir);
        foreach (var id in ExpectedIds)
        {
            var values = store.Load(id!).Values.SelectMany(n => n.Value.Values);
            Assert.That(values, Has.None.Contains(SampleSeeding.PathPlaceholder));
        }
    }

    [Test]
    public void StoreHoldingNoneOfTheSamples_IsUntouched()
    {
        var store = new AnalysisStore(_storeDir);
        store.Create("existing");
        var seeded = SampleSeeding.Seed(store, SamplePaths.AnalysesDir, SamplePaths.TablesDir);
        Assert.That(seeded, Is.Empty);
        Assert.That(store.List().Select(e => e.Id), Is.EqualTo(new[] { "existing" }));
    }

    [Test]
    public void FindRepoRoot_FromTestBinaries_FindsTheSolution()
    {
        var root = SampleSeeding.FindRepoRoot(AppContext.BaseDirectory);
        Assert.That(root, Is.Not.Null);
        Assert.That(Directory.EnumerateFiles(root!, SampleSeeding.SolutionPattern), Is.Not.Empty);
    }

    [Test]
    public void Seed_MissingAnalysesDir_SeedsNothing()
    {
        var store = new AnalysisStore(_storeDir);
        var seeded = SampleSeeding.Seed(store,
            Path.Combine(_storeDir, "no-such-dir"), SamplePaths.TablesDir);
        Assert.That(seeded, Is.Empty);
        Assert.That(store.List(), Is.Empty);
    }
}
