using Ara3D.DataFlowEngine.Abstractions;

namespace BimOpenFlow.Nodes.Spatial;

/// <summary>Polygon-overlap join: every (a, b) pair whose plan polygons share any
/// point. Candidates come from the polygons' bounds; the exact test is edge
/// crossing or containment.</summary>
public sealed class PolygonIntersectsNode : IFlowNode
{
    public const string Kind = "spatial.polygonIntersects";

    public NodeSpec Spec { get; } = new(
        Kind, 1, NodeCapability.Pure,
        Inputs: [new PortSpec("a", PortType.Table), new PortSpec("b", PortType.Table)],
        Outputs: [new PortSpec("table", PortType.Table)],
        Params:
        [
            Shapes.KeyParam(IntersectsNode.AKey, "a"),
            Shapes.KeyParam(IntersectsNode.BKey, "b"),
            Shapes.PolygonColumnParam("a"),
            new(IntersectsNode.ExcludeSelf, ParamKind.Boolean, "true"),
        ],
        "Emits one row per (a, b) pair whose WKT POLYGONs (the 'polygon' column of both "
        + "inputs, default Footprint) intersect in plan, touching included: A and B. With "
        + "excludeSelf, pairs whose two keys are equal are dropped. Rows with an empty polygon "
        + "cell never match; holes are ignored with a warning.");

    public IReadOnlyList<FlowValue> Eval(IEvalContext context, IReadOnlyList<FlowValue> inputs, ParamValues parameters)
    {
        var column = parameters.TextOr(Shapes.PolygonParam, SpatialColumns.Footprint);
        var a = Shapes.ReadPolygons(inputs.TableInput(0, Kind), Kind, parameters.TextOr(IntersectsNode.AKey, SpatialColumns.Name), column, "a", context);
        var b = Shapes.ReadPolygons(inputs.TableInput(1, Kind), Kind, parameters.TextOr(IntersectsNode.BKey, SpatialColumns.Name), column, "b", context);
        var excludeSelf = parameters.GetBoolean(IntersectsNode.ExcludeSelf, true);
        var index = BoxIndex.Build(b.Polygons.Select(p => p?.Bounds).ToList());

        var pairs = new List<Pair>();
        for (var row = 0; row < a.RowCount; row++)
        {
            if (a.Polygons[row] is not { } polygon) continue;
            foreach (var (bRow, _) in index.Candidates(polygon.Bounds))
            {
                var pair = new Pair(row, bRow);
                if (excludeSelf && Pairs.IsSelf(a, b, pair)) continue;
                if (polygon.Intersects(b.Polygons[bRow]!))
                    pairs.Add(pair);
            }
        }
        return [new TableValue(Pairs.Build(a, b, pairs))];
    }
}
