using Ara3D.DataFlowEngine.Abstractions;
using BimOpenFlow.Nodes.Compliance;

namespace BimOpenFlow.Nodes.Compliance.Tests;

[TestFixture]
public sealed class ComplianceNodesTests
{
    [Test]
    public void AllContainsTheFourCheckNodes()
        => Assert.That(ComplianceNodes.All.Select(n => n.Spec.Kind),
            Is.EqualTo(new[] { "check.rule", "check.required", "check.rollup", "check.union" }));

    [Test]
    public void AllNodesAreVersionOneAndPure()
    {
        foreach (var node in ComplianceNodes.All)
        {
            Assert.That(node.Spec.Version, Is.EqualTo(1), node.Spec.Kind);
            Assert.That(node.Spec.Capability, Is.EqualTo(NodeCapability.Pure), node.Spec.Kind);
        }
    }

    [Test]
    public void PackRegistersInANodeRegistry()
    {
        var registry = new NodeRegistry(ComplianceNodes.All);
        Assert.That(registry.Find("check.rule", 1), Is.Not.Null);
        Assert.That(registry.Find("check.union", 1), Is.Not.Null);
        Assert.That(registry.Find("check.rule", 2), Is.Null);
    }

    [Test]
    public void VerdictSeverityOrdersFailWorst()
        => Assert.That(new[] { Verdict.Pass, Verdict.InfoNotAvailable, Verdict.NeedsReview, Verdict.Fail }
                .Select(VerdictExtensions.Severity),
            Is.Ordered.Ascending);
}
