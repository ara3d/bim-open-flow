using Ara3D.DataTable;

namespace BimOpenFlow.Nodes.Spatial;

/// <summary>A matched (a row, b row) of a spatial join.</summary>
public readonly record struct Pair(int ARow, int BRow);

/// <summary>Builds the pairs table every join node emits: column A holds a's key,
/// column B holds b's key (each typed like its source column), followed by the
/// node's measure columns. Row order is the order of the pairs given.</summary>
public static class Pairs
{
    public const string TableName = "pairs";

    public static IDataTable Build(IKeyedSide a, IKeyedSide b, IReadOnlyList<Pair> pairs,
        params (string Name, Array Values)[] measures)
    {
        var builder = new DataTableBuilder(TableName);
        builder.AddColumn(pairs.Select(p => a.Key(p.ARow)).ToArray(), SpatialColumns.A, a.KeyType());
        builder.AddColumn(pairs.Select(p => b.Key(p.BRow)).ToArray(), SpatialColumns.B, b.KeyType());
        foreach (var (name, values) in measures)
            builder.AddColumn(values, name, values.GetType().GetElementType()!);
        return builder.Build();
    }

    /// <summary>True when the pair joins a row to itself: the same row when both sides
    /// are the same table, otherwise the same non-null key. Two rows that merely share a
    /// name in one table are distinct elements and stay paired; null keys never match.</summary>
    public static bool IsSelf(IKeyedSide a, IKeyedSide b, Pair pair)
    {
        if (ReferenceEquals(a.Table, b.Table)) return pair.ARow == pair.BRow;
        var key = a.KeyText(pair.ARow);
        return key != null && string.Equals(key, b.KeyText(pair.BRow), StringComparison.Ordinal);
    }
}
