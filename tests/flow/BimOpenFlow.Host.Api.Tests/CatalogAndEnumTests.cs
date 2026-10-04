using System.Text.Json;
using static BimOpenFlow.Host.Api.Tests.TestGraphs;
using Contracts = BimOpenFlow.Contracts;

namespace BimOpenFlow.Host.Api.Tests;

[TestFixture]
public sealed class CatalogAndEnumTests
{
    [Test]
    public async Task NodeCatalog_DescribesTestNodesWithEnumNames()
    {
        using var doc = await GetJson("/api/catalog/nodes");
        var nodes = doc.RootElement.GetProperty("nodes").EnumerateArray().ToList();

        var effect = nodes.Single(n => n.GetProperty("kind").GetString() == "test.effect");
        Assert.That(effect.GetProperty("capability").GetString(), Is.EqualTo("Effect"));

        var constNode = nodes.Single(n => n.GetProperty("kind").GetString() == "test.const");
        Assert.That(constNode.GetProperty("capability").GetString(), Is.EqualTo("Pure"));
        var kindParam = constNode.GetProperty("params").EnumerateArray()
            .Single(p => p.GetProperty("name").GetString() == "kind");
        Assert.That(kindParam.GetProperty("kind").GetString(), Is.EqualTo("Enum"));
        Assert.That(kindParam.GetProperty("default").GetString(), Is.EqualTo("Integer"));
        Assert.That(kindParam.GetProperty("enumValues").EnumerateArray()
            .Select(v => v.GetString()), Does.Contain("Table"));

        var output = constNode.GetProperty("outputs").EnumerateArray().Single();
        Assert.That(output.GetProperty("type").GetString(), Is.EqualTo("Any"));
    }

    [Test]
    public async Task NodeCatalog_OmitsNullEnumValues()
    {
        var text = await GetOk("/api/catalog/nodes");
        using var doc = JsonDocument.Parse(text);
        var negate = doc.RootElement.GetProperty("nodes").EnumerateArray()
            .Single(n => n.GetProperty("kind").GetString() == "test.negate");
        var inPort = negate.GetProperty("inputs").EnumerateArray().Single();
        Assert.That(inPort.TryGetProperty("enumValues", out _), Is.False);
    }

    // Defensive: contract enums must stay name-identical to their engine-side
    // sources, because all API mapping crosses by name.
    [Test]
    public void ContractEnums_MatchEngineEnumsByName()
    {
        AssertSameNames<Ara3D.DataFlowEngine.NodeStatus, Contracts.NodeStatus>();
        AssertSameNames<Ara3D.DataFlowEngine.Abstractions.NodeCapability, Contracts.NodeCapability>();
        AssertSameNames<Ara3D.DataFlowEngine.Abstractions.ParamKind, Contracts.ParamKind>();
        AssertSameNames<Ara3D.DataFlowEngine.Abstractions.PortType, Contracts.PortType>();
        AssertSameNames<BimOpenFlow.Host.Catalog.ModelKind, Contracts.ModelKind>();
    }

    private static void AssertSameNames<TFrom, TTo>() where TFrom : struct, Enum where TTo : struct, Enum
        => Assert.That(Enum.GetNames<TFrom>(), Is.EqualTo(Enum.GetNames<TTo>()),
            $"{typeof(TFrom).FullName} vs {typeof(TTo).FullName}");
}
