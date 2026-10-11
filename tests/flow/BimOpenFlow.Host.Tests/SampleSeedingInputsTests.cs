using Ara3D.DataFlowEngine.Abstractions;
using Ara3D.NodeGraph;
using BimOpenFlow.Host.Store;

namespace BimOpenFlow.Host.Tests;

/// <summary>TKT-204: a placeholder in a graph input's default is rewritten like one in a
/// parameter value, and a placeholder left in a default keeps the sample out of the store.</summary>
[TestFixture]
public sealed class SampleSeedingInputsTests
{
    private static GraphDocument WithModelInput(string path)
        => GraphDocument.Empty with { Inputs = [new GraphInput("model", ParamKind.FilePath, path)] };

    [Test]
    public void RewritePaths_RewritesInputDefaults()
    {
        var target = Path.Combine(Path.GetTempPath(), "bof-public");
        var rewritten = SampleSeeding.RewritePaths(WithModelInput("{PUBLIC}/a.bos"), "{PUBLIC}", target);
        var expected = Path.GetFullPath(target).Replace('\\', '/').TrimEnd('/') + "/a.bos";
        Assert.That(rewritten.Inputs.Single().Default, Is.EqualTo(expected));
    }

    [Test]
    public void RewritePaths_LeavesOtherPlaceholdersInInputDefaultsAlone()
    {
        var rewritten = SampleSeeding.RewritePaths(WithModelInput("{PUBLIC}/a.bos"), Path.GetTempPath());
        Assert.That(rewritten.Inputs.Single().Default, Is.EqualTo("{PUBLIC}/a.bos"));
    }

    [Test]
    public void Seed_SkipsASampleWhoseInputDefaultKeepsAPlaceholder()
    {
        var root = Path.Combine(Path.GetTempPath(), "bof-seed-inputs-" + Guid.NewGuid().ToString("N"));
        try
        {
            var samples = Path.Combine(root, "demo");
            Directory.CreateDirectory(samples);
            File.WriteAllText(Path.Combine(samples, "m.json"), WithModelInput("{PUBLIC}/a.bos").ToCanonicalJson());
            var log = new StringWriter();
            var seeded = SampleSeeding.Seed(new AnalysisStore(Path.Combine(root, "store")),
                [(samples, SampleSeeding.PathPlaceholder, Path.Combine(root, "t"))], registry: null, log);
            Assert.Multiple(() =>
            {
                Assert.That(seeded, Is.Empty);
                Assert.That(log.ToString(), Does.Contain("unresolved placeholder {PUBLIC}"));
            });
        }
        finally { try { Directory.Delete(root, true); } catch (IOException) { } }
    }
}
