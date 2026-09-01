using BimOpenFlow.Host;

namespace BimOpenFlow.TableWorkflows.Tests;

/// <summary>The profile setting and the registry it selects. These tests need no
/// node bodies, so they pass before the packs are implemented.</summary>
[TestFixture]
public sealed class HostProfileTests
{
    private static readonly string[] ExpectedTableKinds =
    [
        "duck.read", "duck.query", "sql.query",
        "xlsx.read", "sqlite.query",
        "table.join", "table.setOp", "table.project",
        "table.filter", "table.derive", "table.aggregate", "table.sort",
    ];

    [Test]
    public void TablePacks_ContainsExactlyTheTableKinds()
        => Assert.That(
            HostComposition.TablePacks().Nodes.Select(n => n.Spec.Kind),
            Is.EquivalentTo(ExpectedTableKinds));

    [Test]
    public void TablePacks_ContainsNoBimKinds()
        => Assert.That(
            HostComposition.TablePacks().Nodes.Select(n => n.Spec.Kind),
            Has.None.Matches<string>(k =>
                k.StartsWith("bos.") || k.StartsWith("view3d.") || k.StartsWith("check.") || k.StartsWith("sink.")));

    [Test]
    public void DefaultProfile_IsBim()
        => Assert.That(HostConfig.Default(Path.GetTempPath()).Profile, Is.EqualTo("bim"));

    [Test]
    public void ApplyArgs_ProfileTables_RoundTrips()
        => Assert.That(
            HostConfig.Default(Path.GetTempPath()).ApplyArgs(["--profile", "tables"]).Profile,
            Is.EqualTo("tables"));

    [Test]
    public void ApplyArgs_InvalidProfile_ThrowsListingAllowedValues()
        => Assert.That(
            () => HostConfig.Default(Path.GetTempPath()).ApplyArgs(["--profile", "spreadsheets"]),
            Throws.ArgumentException.With.Message.Contains("bim").And.Message.Contains("tables"));

    [Test]
    public void ApplySettingsFile_ReadsProfile()
    {
        var path = Path.Combine(Path.GetTempPath(), $"bimopenflow-profile-{Guid.NewGuid():N}.json");
        File.WriteAllText(path, """{"profile": "tables"}""");
        try
        {
            Assert.That(
                HostConfig.Default(Path.GetTempPath()).ApplySettingsFile(path).Profile,
                Is.EqualTo("tables"));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Test]
    public void ApplyEnvironment_ReadsProfile()
    {
        Environment.SetEnvironmentVariable("BIMOPENFLOW_PROFILE", "tables");
        try
        {
            Assert.That(
                HostConfig.Default(Path.GetTempPath()).ApplyEnvironment().Profile,
                Is.EqualTo("tables"));
        }
        finally
        {
            Environment.SetEnvironmentVariable("BIMOPENFLOW_PROFILE", null);
        }
    }
}
