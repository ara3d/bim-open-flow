using Ara3D.DataFlowEngine.Abstractions;

namespace BimOpenFlow.Nodes.Spatial;

/// <summary>Point-in-polygon join: every (a, polygons) pair where the polygon holds
/// a's point in plan, optionally only the smallest such polygon.</summary>
public sealed class PolygonContainsNode : IFlowNode
{
    public const string Kind = "spatial.polygonContains";

    public NodeSpec Spec { get; } = new(
        Kind, 1, NodeCapability.Pure,
        Inputs: [new PortSpec("a", PortType.Table), new PortSpec("polygons", PortType.Table)],
        Outputs: [new PortSpec("table", PortType.Table)],
        Params:
        [
            Shapes.KeyParam(IntersectsNode.AKey, "a"),
            Shapes.KeyParam(IntersectsNode.BKey, "polygons"),
            .. Shapes.PointParams("a"),
            Shapes.PolygonColumnParam("polygons"),
            new(ContainsNode.Smallest, ParamKind.Boolean, "true"),
        ],
        "Emits one row per (a, polygons) pair where the WKT POLYGON in the 'polygon' column "
        + "(default Footprint) contains a's point in plan, boundary included: A, B, and Area of "
        + "the polygon. a's point is the x, y columns, or the box center when a has MinX..MaxZ "
        + "columns; Z is ignored. With smallest (the default) only the smallest containing "
        + "polygon is kept per a row. Typical use: columns inside zone outlines from a CSV.");

    public IReadOnlyList<FlowValue> Eval(IEvalContext context, IReadOnlyList<FlowValue> inputs, ParamValues parameters)
    {
        var a = Shapes.Read(inputs.TableInput(0, Kind), Kind, parameters.TextOr(IntersectsNode.AKey, SpatialColumns.Name), parameters, "a");
        var b = Shapes.ReadPolygons(inputs.TableInput(1, Kind), Kind,
            parameters.TextOr(IntersectsNode.BKey, SpatialColumns.Name),
            parameters.TextOr(Shapes.PolygonParam, SpatialColumns.Footprint), "polygons", context);
        var smallest = parameters.GetBoolean(ContainsNode.Smallest, true);
        var index = BoxIndex.Build(b.Polygons.Select(p => p?.Bounds).ToList());

        var pairs = new List<Pair>();
        var areas = new List<double>();
        for (var row = 0; row < a.RowCount; row++)
        {
            if (a.Boxes[row] is not { } box) continue;
            var (x, y) = (box.CenterX, box.CenterY);
            var containers = index.Candidates(Box.Point(x, y, 0))
                .Select(c => (c.Row, Polygon: b.Polygons[c.Row]!))
                .Where(c => c.Polygon.Contains(x, y))
                .Select(c => (c.Row, c.Polygon.Area))
                .OrderBy(c => c.Area).ThenBy(c => c.Row)
                .ToList();
            foreach (var c in smallest ? containers.Take(1) : containers.OrderBy(c => c.Row))
            {
                pairs.Add(new(row, c.Row));
                areas.Add(c.Area);
            }
        }
        return [new TableValue(Pairs.Build(a, b, pairs, (SpatialColumns.Area, areas.ToArray())))];
    }
}
