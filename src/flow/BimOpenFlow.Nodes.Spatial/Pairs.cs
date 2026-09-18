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

    /// <summary>True when the pair joins a row to itself by key, which a self-join
    /// reports for every row unless the caller excludes it.</summary>
    public static bool IsSelf(IKeyedSide a, IKeyedSide b, Pair pair)
        => string.Equals(a.KeyText(pair.ARow), b.KeyText(pair.BRow), StringComparison.Ordinal);
}
