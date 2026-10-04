using Ara3D.DataFlowEngine.Abstractions;

namespace BimOpenFlow.Nodes.Spatial;

/// <summary>Box-overlap join: every (a, b) pair whose boxes intersect, with the
/// overlap volume. Axis-aligned-box semantics: pairs are clash candidates, not
/// mesh clashes.</summary>
public sealed class IntersectsNode : IFlowNode
{
    public const string Kind = "spatial.intersects";
    public const string AKey = "aKey";
    public const string BKey = "bKey";
    public const string ExcludeSelf = "excludeSelf";

    public NodeSpec Spec { get; } = new(
        Kind, 1, NodeCapability.Pure,
        Inputs: [new PortSpec("a", PortType.Table), new PortSpec("b", PortType.Table)],
        Outputs: [new PortSpec("table", PortType.Table)],
        Params:
        [
            Shapes.KeyParam(AKey, "a"),
            Shapes.KeyParam(BKey, "b"),
            .. Shapes.PointParams("a"),
            new(ExcludeSelf, ParamKind.Boolean, "true"),
        ],
        "Emits one row per (a, b) pair whose boxes intersect: A ('aKey' of the a row), B "
        + "('bKey' of the b row), and OverlapVolume (zero when the boxes only touch). Each "
        + "input is read as boxes when it has MinX..MaxZ columns, else as points from the "
        + "x, y, z columns. With excludeSelf, pairs whose two keys are equal are dropped, so "
        + "a table joined to itself lists only distinct clash candidates (each unordered "
        + "pair twice, once per direction). Axis-aligned-box semantics: candidates, not mesh "
        + "clashes. Rows with missing coordinates never match.");

    public IReadOnlyList<FlowValue> Eval(IEvalContext context, IReadOnlyList<FlowValue> inputs, ParamValues parameters)
    {
        var a = Shapes.Read(inputs.TableInput(0, Kind), Kind, parameters.TextOr(AKey, SpatialColumns.Name), parameters, "a");
        var b = Shapes.Read(inputs.TableInput(1, Kind), Kind, parameters.TextOr(BKey, SpatialColumns.Name), parameters, "b");
        var excludeSelf = parameters.GetBoolean(ExcludeSelf, true);
        var index = BoxIndex.Build(b.Boxes);

        var pairs = new List<Pair>();
        var volumes = new List<double>();
        for (var row = 0; row < a.RowCount; row++)
        {
            if (a.Boxes[row] is not { } box) continue;
            foreach (var (bRow, candidate) in index.Candidates(box))
            {
                if (!box.Intersects(candidate)) continue;
                var pair = new Pair(row, bRow);
                if (excludeSelf && Pairs.IsSelf(a, b, pair)) continue;
                pairs.Add(pair);
                volumes.Add(box.OverlapVolume(candidate));
            }
        }
        return [new TableValue(Pairs.Build(a, b, pairs, (SpatialColumns.OverlapVolume, volumes.ToArray())))];
    }
}
