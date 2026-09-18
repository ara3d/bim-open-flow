using System.Text.Json;
using static BimOpenFlow.Host.Api.Tests.TestGraphs;

namespace BimOpenFlow.Host.Api.Tests;

[TestFixture]
public sealed class EntityPropertiesTests
{
    private const string WallCategory = "IFCWALLSTANDARDCASE";
    private const string CarbonGroup = "Pset_NRCOperationalCarbon";

    private static string ModelId
    {
        get
        {
            if (ApiTestServer.SampleBosId is null)
                Assert.Ignore("samples/nrc/duplex-enriched.bos not reachable from the test binary.");
            return ApiTestServer.SampleBosId!;
        }
    }

    private static string Path(long localId)
        => $"/api/models/{ModelId}/entities/{localId}/properties";

    [Test]
    public async Task UnknownModel_Returns404ApiError()
    {
        var response = await ApiTestServer.Client.GetAsync("/api/models/no-such-model/entities/1/properties");
        Assert.That((int)response.StatusCode, Is.EqualTo(404));
        using var error = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.That(error.RootElement.GetProperty("error").GetString(), Does.Contain("no-such-model"));
    }

    [Test]
    public async Task UnknownEntity_Returns404ApiError()
    {
        var response = await ApiTestServer.Client.GetAsync(Path(long.MaxValue));
        Assert.That((int)response.StatusCode, Is.EqualTo(404));
        using var error = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.That(error.RootElement.GetProperty("error").GetString(), Does.Contain("not found"));
    }

    [Test]
    public async Task NonNumericLocalId_Returns400()
    {
        var response = await ApiTestServer.Client.GetAsync($"/api/models/{ModelId}/entities/abc/properties");
        Assert.That((int)response.StatusCode, Is.EqualTo(400));
    }

    [Test]
    public async Task EnrichedWall_ReturnsTheNrcCarbonGroupInCamelCase()
    {
        var localId = await FindWallLocalId();
        var text = await GetOk(Path(localId));
        Assert.That(text, Does.Contain("\"localId\"").And.Contain("\"globalId\"").And.Contain("\"parameters\""));

        using var doc = JsonDocument.Parse(text);
        var root = doc.RootElement;
        Assert.That(root.GetProperty("localId").GetInt64(), Is.EqualTo(localId));
        Assert.That(root.GetProperty("category").GetString(), Is.EqualTo(WallCategory));
        Assert.That(root.GetProperty("globalId").GetString(), Is.Not.Empty);

        var parameters = root.GetProperty("parameters").EnumerateArray().ToList();
        Assert.That(parameters.Select(p => p.GetProperty("group").GetString()), Does.Contain(CarbonGroup));
        var carbon = parameters.Single(p =>
            p.GetProperty("group").GetString() == CarbonGroup
            && p.GetProperty("name").GetString() == "OperationalCarbon_kgCO2e_per_year");
        Assert.That(carbon.GetProperty("value").GetString(), Is.Not.Empty);
    }

    /// <summary>The catalog index is the only place the fixture's ids live, so the
    /// test asks it rather than pinning an express id that a re-export could move.</summary>
    private static Task<long> FindWallLocalId()
    {
        var catalog = new Host.Catalog.ModelCatalog(ApiTestServer.ModelsDir,
            System.IO.Path.Combine(ApiTestServer.RootDir, "cache"));
        var entry = catalog.Scan().Single(e => e.Id == ModelId);
        var wall = catalog.GetEntityIndex(entry).All
            .FirstOrDefault(e => e.Category == WallCategory
                && e.Parameters.Any(p => p.Group == CarbonGroup));
        Assert.That(wall, Is.Not.Null, $"no {WallCategory} carries {CarbonGroup}");
        return Task.FromResult(wall!.LocalId);
    }
}
