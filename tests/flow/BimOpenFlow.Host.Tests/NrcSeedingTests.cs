using BimOpenFlow.Host.Store;

namespace BimOpenFlow.Host.Tests;

/// <summary>Both host profiles seed the eight NRC sample graphs into an empty store and
/// register samples/nrc as a model root; its database is a background preparation job,
/// never built inside start-up.</summary>
public sealed class NrcSeedingTests
{
    private static readonly string[] NrcIds =
    [
        "nrc-dc-w1-verdicts", "nrc-enrich-run", "nrc-q1-building-total", "nrc-q3-top-elements",
        "nrc-q5-by-category", "nrc-q7-absence", "nrc-q8-per-storey", "nrc-storey-of-element",
    ];

    private static string NewStoreDir()
        => Path.Combine(Path.GetTempPath(), "bof-nrc-seeding", Guid.NewGuid().ToString("N"));

    [Test]
    public void TablesProfile_SeedsTheNrcGraphs()
        => Assert.That(SampleSeeding.SeedIfEmpty(new AnalysisStore(NewStoreDir()), AppContext.BaseDirectory), Is.SupersetOf(NrcIds));

    [Test]
    public void BimProfile_SeedsTheNrcGraphs()
        => Assert.That(BimSampleSeeding.SeedIfEmpty(new AnalysisStore(NewStoreDir()), AppContext.BaseDirectory), Is.SupersetOf(NrcIds));

    [Test]
    public void SeededRoots_IncludeSamplesNrc_AndItsDatabaseIsAPreparationJob()
    {
        var root = SampleSeeding.FindRepoRoot(AppContext.BaseDirectory)!;
        var nrc = SampleSeeding.NrcSamplesDir(root);
        Assert.Multiple(() =>
        {
            Assert.That(SampleSeeding.SeededModelRoots(AppContext.BaseDirectory), Does.Contain(nrc));
            Assert.That(BimSampleSeeding.SeededModelRoots(AppContext.BaseDirectory), Does.Contain(nrc));
            Assert.That(SamplePreparation.Jobs(AppContext.BaseDirectory).Select(j => j.Output),
                Does.Contain(Path.Combine(nrc, SampleSeeding.NrcDatabaseFileName)));
        });
    }
}
