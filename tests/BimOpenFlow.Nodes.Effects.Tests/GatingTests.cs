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
    public static void EveryNodeThrowsOutsideARun()
    {
        foreach (var node in EffectNodes.All)
            Assert.Throws<InvalidOperationException>(
                () => node.Eval(FakeContext.Design, Array.Empty<FlowValue>(), ParamValues.Empty),
                node.Spec.Kind);
    }
}
