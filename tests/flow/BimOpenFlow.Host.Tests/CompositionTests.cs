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
            var services = HostComposition.BuildServices(TempConfig(root), HostComposition.Tables);
            Assert.Multiple(() =>
            {
                Assert.That(services.Catalog.Roots, Has.Count.EqualTo(1));
                Assert.That(services.Store.RootDir, Does.EndWith("analyses"));
                Assert.That(services.Registry.Nodes, Is.Not.Empty);
                Assert.That(services.Editor.FilePath,
                    Is.EqualTo(Path.Combine(services.Store.RootDir, ".editor-session.json")));
            });
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Test]
    public void Generic_OffersOnlyTheTablesProfile()
        => Assert.Multiple(() =>
        {
            Assert.That(HostComposition.Generic.Names, Is.EqualTo(new[] { "tables" }));
            Assert.That(HostComposition.Generic.Default, Is.EqualTo("tables"));
        });

    [Test]
    public void Profiles_UnknownNameThrowsListingTheAllowedOnes()
        => Assert.That(() => HostComposition.Generic["bim"],
            Throws.ArgumentException.With.Message.Contains("'bim'").And.Message.Contains("tables"));

    [Test]
    public void Config_LayersResolveInOrder()
    {
        var root = Path.Combine(Path.GetTempPath(), "bof-host-cfg-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            File.WriteAllText(Path.Combine(root, HostConfig.SettingsFileName),
                """{"port": 6000, "cacheDir": "fromFile"}""");
            var config = HostConfig.Resolve(["--port", "7000"], root, HostComposition.Generic);
            Assert.Multiple(() =>
            {
                Assert.That(config.Port, Is.EqualTo(7000));
                Assert.That(config.CacheDir, Is.EqualTo("fromFile"));
                Assert.That(config.ModelRoots.Single(), Is.EqualTo(Path.Combine(root, "models")));
                Assert.That(config.Profile, Is.EqualTo("tables"));
            });
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    /// <summary>A folder that does not exist, so no appsettings.json is read from it.</summary>
    private static string NoSettingsDir()
        => Path.Combine(Path.GetTempPath(), "bof-no-settings-" + Guid.NewGuid().ToString("N"));

    [Test]
    public void Resolve_StartsFromTheSetsDefaultProfile()
    {
        var other = HostComposition.Tables with { Name = "other" };
        var config = HostConfig.Resolve([], NoSettingsDir(), new HostProfiles("other", [HostComposition.Tables, other]));
        Assert.That(config.Profile, Is.EqualTo("other"));
    }

    [Test]
    public void Resolve_ProfileOutsideTheSetThrows()
        => Assert.That(() => HostConfig.Resolve(["--profile", "bim"], NoSettingsDir(), HostComposition.Generic),
            Throws.ArgumentException.With.Message.Contains("Allowed values: tables"));

    [Test]
    public void Config_UnknownOptionThrows()
        => Assert.Throws<ArgumentException>(() => HostConfig.Default(".").ApplyArgs(["--bogus", "x"]));
}
