using Ara3D.NodeGraph;
using BimOpenFlow.Host.Store;

namespace BimOpenFlow.Host.Tests;

/// <summary>TKT-83: a sample graph whose parameters still hold a {PLACEHOLDER} the profile
/// named no target directory for (e.g. samples/snowdon-analyses/federation-match.json's
/// {FEDERATION_CONFIRMATIONS} and {FEDERATION_UNION}) is not seeded; every node in it would
/// otherwise error reading a literal "{...}" as a path. The fixture below stands in for that
/// graph without touching the real, untracked sample files.</summary>
[TestFixture]
public sealed class SampleSeedingPlaceholderTests
{
    private string _root = null!;

    [SetUp]
    public void SetUp()
    {
        _root = Path.Combine(Path.GetTempPath(), "bof-seed-placeholder-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
    }

    [TearDown]
    public void TearDown()
    {
        try { Directory.Delete(_root, recursive: true); }
        catch (IOException) { }
    }

    /// <summary>A csv.read node whose path names {SAMPLES} (which the seeder resolves) beside a
    /// second, unrelated {FEDERATION_CONFIRMATIONS} placeholder that nothing resolves.</summary>
    private static GraphDocument OneUnresolvedParam()
        => GraphDocument.Empty
            .AddNode("read", "csv.read", 1)
            .SetParam("read", "path", "{SAMPLES}/walls.csv")
            .AddNode("other", "csv.read", 1)
            .SetParam("other", "path", "{FEDERATION_CONFIRMATIONS}");

    private static GraphDocument FullyResolved()
        => GraphDocument.Empty
            .AddNode("read", "csv.read", 1)
            .SetParam("read", "path", "{SAMPLES}/walls.csv");

    [Test]
    public void AGraphWithAnUnresolvedPlaceholder_IsSkippedAndLogged()
    {
        var analysesDir = Path.Combine(_root, "analyses");
        Directory.CreateDirectory(analysesDir);
        File.WriteAllText(Path.Combine(analysesDir, "federation-match.json"), OneUnresolvedParam().ToCanonicalJson());

        var log = new StringWriter();
        var seeded = SampleSeeding.Seed(new AnalysisStore(Path.Combine(_root, "store")),
            [(analysesDir, SampleSeeding.PathPlaceholder, Path.Combine(_root, "tables"))], registry: null, log);

        Assert.That(seeded, Is.Empty);
        Assert.That(log.ToString(), Does.Contain("skipped sample analysis federation-match"));
        Assert.That(log.ToString(), Does.Contain("{FEDERATION_CONFIRMATIONS}"));
    }

    [Test]
    public void AGraphWhosePlaceholdersAllResolve_IsSeeded()
    {
        var analysesDir = Path.Combine(_root, "analyses");
        Directory.CreateDirectory(analysesDir);
        File.WriteAllText(Path.Combine(analysesDir, "ok-graph.json"), FullyResolved().ToCanonicalJson());

        var log = new StringWriter();
        var seeded = SampleSeeding.Seed(new AnalysisStore(Path.Combine(_root, "store")),
            [(analysesDir, SampleSeeding.PathPlaceholder, Path.Combine(_root, "tables"))], registry: null, log);

        Assert.That(seeded, Is.EqualTo(new[] { "ok-graph" }));
        Assert.That(log.ToString(), Is.Empty);
    }
}
