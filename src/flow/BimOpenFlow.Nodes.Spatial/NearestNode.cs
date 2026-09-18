using Ara3D.DataFlowEngine.Abstractions;

namespace BimOpenFlow.Nodes.Spatial;

/// <summary>k-nearest join: for each a row, the k closest b rows with their
/// distance and rank. Searches the index in an expanding radius, so no a row
/// scans all of b unless b is small or the answer needs it.</summary>
public sealed class NearestNode : IFlowNode
{
    public const string Kind = "spatial.nearest";
    public const string K = "k";

    /// <summary>Below this many indexed rows a plain scan beats the radius search.</summary>
    private const int ScanThreshold = 64;

    public NodeSpec Spec { get; } = new(
        Kind, 1, NodeCapability.Pure,
        Inputs: [new PortSpec("a", PortType.Table), new PortSpec("b", PortType.Table)],
        Outputs: [new PortSpec("table", PortType.Table)],
        Params:
        [
            new(K, ParamKind.Integer, "1"),
            Measures.Spec,
            Shapes.KeyParam(IntersectsNode.AKey, "a"),
            Shapes.KeyParam(IntersectsNode.BKey, "b"),
            .. Shapes.PointParams("a"),
            new(IntersectsNode.ExcludeSelf, ParamKind.Boolean, "true"),
        ],
        "Emits, for each a row, its 'k' nearest b rows: A, B, Distance, and Rank (1 = closest). "
        + "With measure = box the distance is between the boxes' surfaces (zero when they "
        + "intersect); with center it is between their centers. Ties keep b's row order. Each "
        + "input is read as boxes when it has MinX..MaxZ columns, else as points from the x, y, z "
        + "columns. excludeSelf drops the b row whose key equals a's. An a row with missing "
        + "coordinates, or an empty b, produces no rows. Typical use: the three nearest fire "
        + "extinguishers to each room.");

    public IReadOnlyList<FlowValue> Eval(IEvalContext context, IReadOnlyList<FlowValue> inputs, ParamValues parameters)
    {
        var a = Shapes.Read(inputs.TableInput(0, Kind), Kind, parameters.TextOr(IntersectsNode.AKey, SpatialColumns.Name), parameters, "a");
        var b = Shapes.Read(inputs.TableInput(1, Kind), Kind, parameters.TextOr(IntersectsNode.BKey, SpatialColumns.Name), parameters, "b");
        var k = parameters.GetInteger(K, 1);
        if (k < 1)
            throw new ArgumentException($"{Kind}: parameter '{K}' must be at least 1.");
        var measure = Measures.Read(parameters, Kind);
        var excludeSelf = parameters.GetBoolean(IntersectsNode.ExcludeSelf, true);
        var index = BoxIndex.Build(b.Boxes);

        var pairs = new List<Pair>();
        var distances = new List<double>();
        var ranks = new List<long>();
        for (var row = 0; row < a.RowCount; row++)
        {
            if (a.Boxes[row] is not { } box) continue;
            var rank = 0L;
            foreach (var (bRow, d) in Nearest(index, box, measure, (int)k, bRow => excludeSelf && Pairs.IsSelf(a, b, new(row, bRow))))
            {
                pairs.Add(new(row, bRow));
                distances.Add(d);
                ranks.Add(++rank);
            }
        }
        return [new TableValue(Pairs.Build(a, b, pairs,
            (SpatialColumns.Distance, distances.ToArray()), (SpatialColumns.Rank, ranks.ToArray())))];
    }

    /// <summary>The k closest indexed rows to the box, nearest first, ties in row order.
    /// Surface distance never exceeds center distance, so candidates within radius r by
    /// surface contain every row within r by either measure.</summary>
    private static IReadOnlyList<(int Row, double Distance)> Nearest(
        BoxIndex index, Box box, string measure, int k, Func<int, bool> exclude)
    {
        if (index.TotalBounds is not { } total) return [];
        IReadOnlyList<(int Row, double Distance)> Measured(IReadOnlyList<(int Row, Box Box)> rows, double? radius)
            => rows.Where(r => !exclude(r.Row))
                .Select(r => (r.Row, Distance: Measures.Distance(measure, box, r.Box)))
                .Where(r => radius == null || r.Distance <= radius)
                .OrderBy(r => r.Distance).ThenBy(r => r.Row)
                .Take(k)
                .ToList();
        if (index.Count <= ScanThreshold)
            return Measured(index.All(), null);

        var diagonal = Math.Sqrt(total.SizeX * total.SizeX + total.SizeY * total.SizeY + total.SizeZ * total.SizeZ);
        var radius = Math.Max(diagonal / 16, 1e-9);
        while (true)
        {
            var reach = box.Expand(radius);
            var found = Measured(index.Candidates(reach), radius);
            if (found.Count >= k || reach.Contains(total)) return found;
            radius *= 2;
        }
    }
}
