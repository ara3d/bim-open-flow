using Ara3D.DataFlowEngine.Abstractions;

namespace BimOpenFlow.Nodes.Spatial;

/// <summary>Containment join: every (a, box) pair where the b box holds all of a's
/// box or point, optionally only the smallest such container. The ST_Contains of
/// the pack, with b as the container side.</summary>
public sealed class ContainsNode : IFlowNode
{
    public const string Kind = "spatial.contains";
    public const string Smallest = "smallest";
    public const string IgnoreZ = "ignoreZ";

    public NodeSpec Spec { get; } = new(
        Kind, 1, NodeCapability.Pure,
        Inputs: [new PortSpec("a", PortType.Table), new PortSpec("boxes", PortType.Table)],
        Outputs: [new PortSpec("table", PortType.Table)],
        Params:
        [
            Shapes.KeyParam(IntersectsNode.AKey, "a"),
            Shapes.KeyParam(IntersectsNode.BKey, "boxes"),
            .. Shapes.PointParams("a"),
            new(Smallest, ParamKind.Boolean, "true"),
            new(IgnoreZ, ParamKind.Boolean, "false"),
            new(IntersectsNode.ExcludeSelf, ParamKind.Boolean, "true"),
        ],
        "Emits one row per (a, boxes) pair where the boxes row's box contains a's box or point "
        + "entirely: A, B, and ContainerVolume. With smallest (the default) only the smallest "
        + "container of each a row is kept, so the output has at most one row per a row; with "
        + "ignoreZ containment and smallest are judged in plan (XY) only. a is read as boxes when "
        + "it has MinX..MaxZ columns, else as points from the x, y, z columns. excludeSelf drops "
        + "pairs whose keys are equal. Rows in no box produce nothing. Typical use: element "
        + "centers from bim.bounds against room boxes from bim.rooms.");

    public IReadOnlyList<FlowValue> Eval(IEvalContext context, IReadOnlyList<FlowValue> inputs, ParamValues parameters)
    {
        var a = Shapes.Read(inputs.TableInput(0, Kind), Kind, parameters.TextOr(IntersectsNode.AKey, SpatialColumns.Name), parameters, "a");
        var boxes = inputs.TableInput(1, Kind);
        if (!Shapes.HasBoxColumns(boxes))
            throw new ArgumentException($"{Kind}: input 'boxes' must have the box columns MinX..MaxZ.");
        var b = Shapes.Read(boxes, Kind, parameters.TextOr(IntersectsNode.BKey, SpatialColumns.Name), parameters, "boxes");
        var smallest = parameters.GetBoolean(Smallest, true);
        var ignoreZ = parameters.GetBoolean(IgnoreZ);
        var excludeSelf = parameters.GetBoolean(IntersectsNode.ExcludeSelf, true);
        var index = BoxIndex.Build(b.Boxes);
        var total = index.TotalBounds;

        var pairs = new List<Pair>();
        var volumes = new List<double>();
        for (var row = 0; row < a.RowCount; row++)
        {
            if (a.Boxes[row] is not { } box || total is not { } bounds) continue;
            var query = ignoreZ ? box.WithZ(bounds.MinZ, bounds.MaxZ) : box;
            var containers = index.Candidates(query)
                .Where(c => c.Box.Contains(box, ignoreZ))
                .Where(c => !excludeSelf || !Pairs.IsSelf(a, b, new(row, c.Row)))
                .Select(c => (c.Row, c.Box, Measure: ignoreZ ? c.Box.FootprintArea : c.Box.Volume))
                .OrderBy(c => c.Measure).ThenBy(c => c.Row)
                .ToList();
            foreach (var c in smallest ? containers.Take(1) : containers.OrderBy(c => c.Row))
            {
                pairs.Add(new(row, c.Row));
                volumes.Add(c.Box.Volume);
            }
        }
        return [new TableValue(Pairs.Build(a, b, pairs, (SpatialColumns.ContainerVolume, volumes.ToArray())))];
    }
}
