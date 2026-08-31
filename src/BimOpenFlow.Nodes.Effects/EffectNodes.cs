using Ara3D.DataFlowEngine.Abstractions;

namespace BimOpenFlow.Nodes.Effects;

/// <summary>The effects node pack: every Run-gated sink.</summary>
public static class EffectNodes
{
    public static IReadOnlyList<IFlowNode> All { get; } =
        new IFlowNode[]
        {
            new ExportCsvNode(),
            new WritePsetsNode(),
            new ReportNode(),
        };
}
