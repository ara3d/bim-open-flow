using Ara3D.DataFlowEngine.Abstractions;
using BimOpenFlow.Nodes.Effects;

namespace BimOpenFlow.Nodes.Effects.Tests;

public static class GatingTests
{
    [Test]
    public static void EveryNodeIsVersionOneEffectWithSinkPrefix()
    {
        Assert.That(EffectNodes.All, Is.Not.Empty);
        foreach (var node in EffectNodes.All)
        {
            Assert.That(node.Spec.Kind, Does.StartWith("sink."));
            Assert.That(node.Spec.Version, Is.EqualTo(1));
            Assert.That(node.Spec.Capability, Is.EqualTo(NodeCapability.Effect));
        }
    }

    [Test]
    public static void TableSinksAreTheSixWritersAndPartOfAll()
    {
        Assert.That(EffectNodes.TableSinks.Select(n => n.Spec.Kind), Is.EqualTo(new[]
        {
            "sink.exportCsv", "sink.exportParquet", "sink.exportJson",
            "sink.exportXlsx", "sink.exportSqlite", "sink.exportDuckDb",
        }));
        Assert.That(EffectNodes.TableSinks.Select(n => n.Spec.Kind),
            Is.SubsetOf(EffectNodes.All.Select(n => n.Spec.Kind)));
    }

    [Test]
    public static void EveryEnumParamDeclaresItsValues()
    {
        foreach (var node in EffectNodes.All)
            foreach (var param in node.Spec.Params)
                if (param.Kind == ParamKind.Enum)
                    Assert.That(param.EnumValues, Is.Not.Null.And.Not.Empty,
                        $"{node.Spec.Kind}.{param.Name}");
    }

    [Test]
    public static void EveryNodeThrowsOutsideARun()
    {
        foreach (var node in EffectNodes.All)
            Assert.Throws<InvalidOperationException>(
                () => node.Eval(FakeContext.Design, Array.Empty<FlowValue>(), ParamValues.Empty),
                node.Spec.Kind);
    }
}
