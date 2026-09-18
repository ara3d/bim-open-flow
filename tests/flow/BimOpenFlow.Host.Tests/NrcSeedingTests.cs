using BimOpenFlow.Host.Store;
using BimOpenToolkit.TestSupport;

namespace BimOpenFlow.Host.Tests;

/// <summary>Both host profiles seed the NRC sample graphs their registry can run into an
/// empty store and register samples/nrc as a model root; its database and BOS file are
/// background preparation jobs, never built inside start-up.</summary>
public sealed class NrcSeedingTests
{
    private static readonly string[] SharedIds =
    [
        "nrc-q1-building-total", "nrc-q3-top-elements", "nrc-q5-by-category", "nrc-q7-absence",
        "nrc-q8-per-storey", "nrc-storey-of-element",
    ];

    /// <summary>check.rule, view3d.color, and sink.writePsets exist only in the bim profile.</summary>
    private static readonly string[] BimOnlyIds = ["nrc-dc-w1-verdicts", "nrc-enrich-run"];

    private static string NewStoreDir()
        => Path.Combine(Path.GetTempPath(), "bof-nrc-seeding", Guid.NewGuid().ToString("N"));

    [Test]
    public void TablesProfile_SeedsTheNrcGraphsItCanRun_AndReportsTheOthers()
    {
        var log = new StringWriter();
        var seeded = SampleSeeding.SeedIfEmpty(new AnalysisStore(NewStoreDir()), AppContext.BaseDirectory,
            HostComposition.TablePacks(), log);
        Assert.Multiple(() =>
        {
            Assert.That(seeded, Is.SupersetOf(SharedIds));
            Assert.That(seeded, Has.None.AnyOf(BimOnlyIds));
            Assert.That(log.ToString(), Does.Contain("skipped sample analysis nrc-dc-w1-verdicts")
                .And.Contain("skipped sample analysis nrc-enrich-run"));
        });
    }

    [Test]
    public void BimProfile_SeedsEveryNrcGraph()
    {
        var log = new StringWriter();
        var seeded = BimSampleSeeding.SeedIfEmpty(new AnalysisStore(NewStoreDir()), AppContext.BaseDirectory,
            HostComposition.AllPacks(), log);
        Assert.Multiple(() =>
        {
            Assert.That(seeded, Is.SupersetOf(SharedIds.Concat(BimOnlyIds)));
            Assert.That(log.ToString(), Is.Empty, "every bim sample validates against the bim registry");
        });
    }

    [Test]
    public void WithoutARegistry_EverythingIsSeeded()
        => Assert.That(SampleSeeding.SeedIfEmpty(new AnalysisStore(NewStoreDir()), AppContext.BaseDirectory),
            Is.SupersetOf(SharedIds.Concat(BimOnlyIds)));

    [Test]
    public void SeededRoots_IncludeSamplesNrc_AndItsDatabaseIsAPreparationJob()
    {
        var root = RepoPaths.Root;
        var nrc = SampleSeeding.NrcSamplesDir(root);
        Assert.Multiple(() =>
        {
            Assert.That(SampleSeeding.SeededModelRoots(AppContext.BaseDirectory), Does.Contain(nrc));
            Assert.That(BimSampleSeeding.SeededModelRoots(AppContext.BaseDirectory), Does.Contain(nrc));
            Assert.That(SamplePreparation.Jobs(AppContext.BaseDirectory).Select(j => j.Output),
                Is.SupersetOf(new[]
                {
                    Path.Combine(nrc, SampleSeeding.NrcDatabaseFileName),
                    Path.Combine(nrc, SampleSeeding.NrcBosFileName),
                }));
        });
    }
}
