using Ara3D.DataFlowEngine.Abstractions;

namespace BimOpenFlow.Nodes.Compliance;

/// <summary>The compliance node pack: the verdict-bearing vocabulary.</summary>
public static class ComplianceNodes
{
    public static IReadOnlyList<IFlowNode> All { get; } =
        new IFlowNode[]
        {
            new CheckRuleNode(),
            new CheckRequiredNode(),
            new CheckRollupNode(),
            new CheckUnionNode(),
        };
}
