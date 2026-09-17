using BimOpenFlow.Host;

namespace BimOpenFlow.Host.Tests;

public sealed class CompositionTests
{
    private static HostConfig TempConfig(string root)
        => new([Path.Combine(root, "models")], Path.Combine(root, "cache"),
            Path.Combine(root, "analyses"), Port: 0);

    [Test]
    public void BuildServices_WiresWithoutThrowing()
    {
        var root = Path.Combine(Path.GetTempPath(), "bof-host-comp-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, "models"));
        try
        {
            var services = HostComposition.BuildServices(TempConfig(root));
            Assert.Multiple(() =>
            {
                Assert.That(services.Catalog.Roots, Has.Count.EqualTo(1));
                Assert.That(services.Store.RootDir, Does.EndWith("analyses"));
                Assert.That(services.Registry.Nodes, Is.Not.Empty);
            });
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [TestCase("bos.load")]
    [TestCase("view3d.instances")]
    [TestCase("check.rule")]
    [TestCase("sink.exportCsv")]
    public void Registry_ContainsEachPack(string kind)
        => Assert.That(HostComposition.AllPacks().Find(kind, 1), Is.Not.Null, kind);

    [Test]
    public void Config_LayersResolveInOrder()
    {
        var root = Path.Combine(Path.GetTempPath(), "bof-host-cfg-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            File.WriteAllText(Path.Combine(root, HostConfig.SettingsFileName),
                """{"port": 6000, "cacheDir": "fromFile"}""");
            var config = HostConfig.Resolve(["--port", "7000"], root);
            Assert.Multiple(() =>
            {
                Assert.That(config.Port, Is.EqualTo(7000));
                Assert.That(config.CacheDir, Is.EqualTo("fromFile"));
                Assert.That(config.ModelRoots.Single(), Is.EqualTo(Path.Combine(root, "models")));
            });
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Test]
    public void Config_UnknownOptionThrows()
        => Assert.Throws<ArgumentException>(() => HostConfig.Default(".").ApplyArgs(["--bogus", "x"]));
}
