using Ara3D.DataFlowEngine.Abstractions;

namespace BimOpenFlow.Nodes.Spatial;

/// <summary>The spatial.* pack: GIS-style predicates and measures over the point,
/// box, and WKT polygon column conventions. BIM-free; every value is a plain table.</summary>
public static class SpatialNodes
{
    public static IReadOnlyList<IFlowNode> All { get; } =
    [
        new IntersectsNode(),
    ];
}
