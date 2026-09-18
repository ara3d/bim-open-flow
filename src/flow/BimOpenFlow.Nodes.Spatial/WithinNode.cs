using Ara3D.DataFlowEngine.Abstractions;

namespace BimOpenFlow.Nodes.Spatial;

/// <summary>Distance join: every (a, b) pair whose boxes lie within a distance of
/// each other, with that distance. The ST_DWithin of the pack.</summary>
public sealed class WithinNode : IFlowNode
{
    public const string Kind = "spatial.within";
    public const string DistanceParam = "distance";

    public NodeSpec Spec { get; } = new(
        Kind, 1, NodeCapability.Pure,
        Inputs: [new PortSpec("a", PortType.Table), new PortSpec("b", PortType.Table)],
        Outputs: [new PortSpec("table", PortType.Table)],
        Params:
        [
            new(DistanceParam, ParamKind.Number),
            Measures.Spec,
            Shapes.KeyParam(IntersectsNode.AKey, "a"),
            Shapes.KeyParam(IntersectsNode.BKey, "b"),
            .. Shapes.PointParams("a"),
            new(IntersectsNode.ExcludeSelf, ParamKind.Boolean, "true"),
        ],
        "Emits one row per (a, b) pair whose boxes are at most 'distance' apart: A, B, and "
        + "Distance. With measure = box the distance is between the boxes' surfaces (zero when "
        + "they intersect); with center it is between their centers. Each input is read as "
        + "boxes when it has MinX..MaxZ columns, else as points from the x, y, z columns. "
        + "excludeSelf drops pairs whose keys are equal. Axis-aligned-box semantics: candidates, "
        + "not clearances. Typical use: rooms within 3 m of a stair.");

    public IReadOnlyList<FlowValue> Eval(IEvalContext context, IReadOnlyList<FlowValue> inputs, ParamValues parameters)
    {
        var a = Shapes.Read(inputs.TableInput(0, Kind), Kind, parameters.TextOr(IntersectsNode.AKey, SpatialColumns.Name), parameters, "a");
        var b = Shapes.Read(inputs.TableInput(1, Kind), Kind, parameters.TextOr(IntersectsNode.BKey, SpatialColumns.Name), parameters, "b");
        var distance = parameters.RequiredNumber(DistanceParam, Kind);
        if (!(distance >= 0))
            throw new ArgumentException($"{Kind}: parameter '{DistanceParam}' must be zero or more.");
        var measure = Measures.Read(parameters, Kind);
        var excludeSelf = parameters.GetBoolean(IntersectsNode.ExcludeSelf, true);
        var index = BoxIndex.Build(b.Boxes);

        var pairs = new List<Pair>();
        var distances = new List<double>();
        for (var row = 0; row < a.RowCount; row++)
        {
            if (a.Boxes[row] is not { } box) continue;
            foreach (var (bRow, candidate) in index.Candidates(box.Expand(distance)))
            {
                var d = Measures.Distance(measure, box, candidate);
                if (d > distance) continue;
                var pair = new Pair(row, bRow);
                if (excludeSelf && Pairs.IsSelf(a, b, pair)) continue;
                pairs.Add(pair);
                distances.Add(d);
            }
        }
        return [new TableValue(Pairs.Build(a, b, pairs, (SpatialColumns.Distance, distances.ToArray())))];
    }
}
