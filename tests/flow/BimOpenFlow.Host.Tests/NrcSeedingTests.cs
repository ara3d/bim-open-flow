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
        "nrc-q8-per-storey", "nrc-storey-of-element", "nrc-storey-carbon-chart", "nrc-property-values",
        "nrc-element-psets", "nrc-rollup",
    ];

    /// <summary>check.rule, view3d.instances, view3d.color, and sink.writePsets exist only in the bim profile.</summary>
    private static readonly string[] BimOnlyIds =
    [
        "nrc-dc-w1-verdicts", "nrc-enrich-run",
        "nrc-color-operational-carbon",
    ];

    private static string NewStoreDir()
        => Path.Combine(Path.GetTempPath(), "bof-nrc-seeding", Guid.NewGuid().ToString("N"));

    [Test]
    public void TablesProfile_SeedsTheNrcGraphsItCanRun_AndReportsTheOthers()
    {
        var log = new StringWriter();
        var seeded = SampleSeeding.Seed(new AnalysisStore(NewStoreDir()), AppContext.BaseDirectory,
            HostComposition.TablePacks(), log);
        Assert.Multiple(() =>
        {
            Assert.That(seeded, Is.SupersetOf(SharedIds));
            Assert.That(seeded, Has.None.AnyOf(BimOnlyIds));
            foreach (var id in BimOnlyIds)
                Assert.That(log.ToString(), Does.Contain($"skipped sample analysis {id}"));
        });
    }

    [Test]
    public void BimProfile_SeedsEveryNrcGraph()
    {
        var log = new StringWriter();
        var seeded = BimSampleSeeding.Seed(new AnalysisStore(NewStoreDir()), AppContext.BaseDirectory,
            HostComposition.AllPacks(), log);
        Assert.Multiple(() =>
        {
            Assert.That(seeded, Is.SupersetOf(SharedIds.Concat(BimOnlyIds)));
            // Every NRC sample validates against the bim registry. On a machine with the local
            // Snowdon model, samples/snowdon-analyses/federation-match.json also seeds; its
            // {FEDERATION_CONFIRMATIONS} and {FEDERATION_UNION} placeholders resolve to no
            // target directory the profile names, so TKT-83's unresolved-placeholder check skips
            // it rather than seed a graph whose nodes would fail reading a literal "{...}" path.
            var lines = log.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries);
            Assert.That(lines, Has.All.Contains("skipped sample analysis federation-match"));
        });
    }

    [Test]
    public void WithoutARegistry_EverythingIsSeeded()
        => Assert.That(SampleSeeding.Seed(new AnalysisStore(NewStoreDir()), AppContext.BaseDirectory),
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
