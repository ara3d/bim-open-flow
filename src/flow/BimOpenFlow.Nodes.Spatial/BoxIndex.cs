using Ara3D.Geometry;

namespace BimOpenFlow.Nodes.Spatial;

/// <summary>An AABB tree over one side's boxes, answering candidate queries by row.
/// Candidates are found in single precision on padded bounds; callers decide the
/// predicate exactly on the returned Box.</summary>
public sealed class BoxIndex
{
    private readonly AabbTree? _tree;
    private readonly int[] _rows;
    private readonly Box[] _boxes;

    private BoxIndex(AabbTree? tree, int[] rows, Box[] boxes)
    {
        _tree = tree;
        _rows = rows;
        _boxes = boxes;
        TotalBounds = boxes.Length == 0 ? null : boxes.Skip(1).Aggregate(boxes[0], (u, b) => u.Union(b));
    }

    public static BoxIndex Build(IReadOnlyList<Box?> boxes)
    {
        var rows = Enumerable.Range(0, boxes.Count).Where(i => boxes[i] != null).ToArray();
        var kept = rows.Select(i => boxes[i]!.Value).ToArray();
        var tree = kept.Length == 0 ? null : kept.Select(b => b.ToBounds3D()).ToList().ToAabbTree();
        return new(tree, rows, kept);
    }

    public int Count => _rows.Length;

    /// <summary>The union of every indexed box, or null when nothing is indexed.</summary>
    public Box? TotalBounds { get; }

    /// <summary>Indexed rows whose padded bounds overlap the query's, in row order.</summary>
    public IReadOnlyList<(int Row, Box Box)> Candidates(Box query)
    {
        if (_tree == null) return [];
        var hits = _tree.QueryOverlaps(query.ToBounds3D());
        hits.Sort();
        return hits.Select(i => (_rows[i], _boxes[i])).ToList();
    }

    /// <summary>Every indexed row, for callers that must fall back to a full scan.</summary>
    public IReadOnlyList<(int Row, Box Box)> All()
        => Enumerable.Range(0, _rows.Length).Select(i => (_rows[i], _boxes[i])).ToList();
}
