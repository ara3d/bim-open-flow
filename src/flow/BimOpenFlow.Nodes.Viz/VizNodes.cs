using Ara3D.DataFlowEngine.Abstractions;

namespace BimOpenFlow.Nodes.Viz;

/// <summary>The visualization pack: chart and table-view nodes that validate
/// and project table data for the web panes. Rendering stays client-side
/// (@bimopenflow/viz); these nodes only shape what gets rendered.</summary>
public static class VizNodes
{
    public static IReadOnlyList<IFlowNode> All { get; } =
        [new ChartBarNode(), new ChartLineNode(), new ViewTableNode()];
}
