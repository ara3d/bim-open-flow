using Ara3D.DataFlowEngine.Expressions;

namespace BimOpenFlow.Relations;

public static partial class SchemaRules
{
    /// <summary>The group columns (as the input spells them), then one column per aggregation. Count is
    /// Integer; sum keeps Integer or Number and fails on anything else; avg is Number; min and max keep the
    /// column's type except for Boolean, Binary and Unknown. Output names must be distinct.</summary>
    public static SchemaResult Aggregate(Schema input, IReadOnlyList<string> groupBy, IReadOnlyList<Aggregation> aggregates)
    {
        if (RequireAll(input, groupBy) is { } missing) return missing;
        if (RequireAll(input, aggregates.Where(x => x.Column is not null).Select(x => x.Column!)) is { } missingAgg) return missingAgg;
        var columns = groupBy.Select(g => input.Find(g)!.Value).ToList();
        foreach (var agg in aggregates)
        {
            var source = agg.Column is null ? (Column?)null : input.Find(agg.Column);
            var type = AggregateType(agg.Function, source?.Type);
            if (type is null)
                return SchemaResult.Fail($"Cannot apply {agg.Function} to '{agg.Column}' of type {source?.Type}.");
            columns.Add(new Column(agg.Name, type.Value, Nullable: agg.Function != AggregateFunction.Count));
        }
        return Projection.FirstDuplicate(columns.Select(c => c.Name)) is { } clash
            ? SchemaResult.Fail($"Aggregate produces duplicate column '{clash}'.")
            : SchemaResult.Of(new Schema(columns));
    }

    private static ColumnType? AggregateType(AggregateFunction function, ColumnType? source)
        => function switch
        {
            AggregateFunction.Count => ColumnType.Integer,
            AggregateFunction.Sum when source is ColumnType.Integer or ColumnType.Number => source,
            AggregateFunction.Avg when source is ColumnType.Integer or ColumnType.Number => ColumnType.Number,
            AggregateFunction.Min or AggregateFunction.Max when source is not (null or ColumnType.Boolean or ColumnType.Binary or ColumnType.Unknown) => source,
            _ => null,
        };
}
