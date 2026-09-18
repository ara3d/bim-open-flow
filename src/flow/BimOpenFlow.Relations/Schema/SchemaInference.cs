using Ara3D.DataFlowEngine.Expressions;
using Ara3D.DataFlowEngine.Expressions.Typing;

namespace BimOpenFlow.Relations;

/// <summary>One inference rule per operator. Errors are values; the first failing
/// input stops inference for everything above it.</summary>
public static class SchemaInference
{
    public const string RightSuffix = "_right";

    public static SchemaResult Infer(this Plan plan, ICatalog catalog)
        => new SchemaCache(catalog).Infer(plan);

    internal static SchemaResult InferNode(Plan plan, IReadOnlyList<SchemaResult> inputs, ICatalog catalog)
    {
        var failed = inputs.FirstOrDefault(r => !r.Ok);
        if (failed is not null) return failed;
        var schemas = inputs.Select(r => r.Schema!).ToList();
        return plan switch
        {
            ReadCsv r => catalog.CsvSchema(r.Source, r.Path),
            ReadTable r => catalog.TableSchema(r.Source, r.Table),
            InlineTable t => SchemaResult.Of(t.Schema),
            RawSql r => catalog.QuerySchema(r.Sql, schemas),
            Select s => InferSelect(s, schemas[0]),
            Rename r => InferRename(r, schemas[0]),
            Cast c => Require(schemas[0], c.Column, col => SchemaResult.Of(schemas[0].Map(x => x.Name == col.Name ? x with { Type = c.Type } : x))),
            Derive d => InferDerive(d, schemas[0]),
            Filter f => InferFilter(f, schemas[0]),
            Distinct => SchemaResult.Of(schemas[0]),
            Sort s => RequireAll(schemas[0], s.Keys.Select(k => k.Column)) ?? SchemaResult.Of(schemas[0]),
            Limit => SchemaResult.Of(schemas[0]),
            Join j => InferJoin(j, schemas[0], schemas[1]),
            Union => InferUnion(schemas),
            Aggregate a => InferAggregate(a, schemas[0]),
            _ => SchemaResult.Fail($"No inference rule for {plan.GetType().Name}."),
        };
    }

    private static SchemaResult InferSelect(Select s, Schema input)
        => RequireAll(input, s.Columns) ?? SchemaResult.Of(new Schema(s.Columns.Select(c => input.Find(c)!.Value).ToList()));

    private static SchemaResult InferRename(Rename r, Schema input)
    {
        if (RequireAll(input, r.Renamings.Select(x => x.From)) is { } missing) return missing;
        var map = r.Renamings.ToDictionary(x => input.Find(x.From)!.Value.Name, x => x.To);
        var renamed = input.Map(c => map.TryGetValue(c.Name, out var to) ? c with { Name = to } : c);
        var clash = renamed.Columns.GroupBy(c => c.Name, StringComparer.OrdinalIgnoreCase).FirstOrDefault(g => g.Count() > 1);
        return clash is null ? SchemaResult.Of(renamed) : SchemaResult.Fail($"Rename produces duplicate column '{clash.Key}'.");
    }

    private static SchemaResult InferDerive(Derive d, Schema input)
    {
        if (input.Has(d.Name)) return SchemaResult.Fail($"Column '{d.Name}' already exists.");
        var (type, errors) = d.Expression.TypeOf(input);
        return errors.Count > 0
            ? SchemaResult.Fail(errors.Select(e => new SchemaError($"In '{d.Name}': {e}")).ToList())
            : SchemaResult.Of(input.Append(new Column(d.Name, type.ToColumnType())));
    }

    private static SchemaResult InferFilter(Filter f, Schema input)
    {
        var (type, errors) = f.Predicate.TypeOf(input);
        return errors.Count > 0 ? SchemaResult.Fail(errors.Select(e => new SchemaError($"In predicate: {e}")).ToList())
            : type is not (null or ScalarType.Boolean) ? SchemaResult.Fail($"Predicate must be Boolean, but it is {type}.")
            : SchemaResult.Of(input);
    }

    private static SchemaResult InferJoin(Join j, Schema left, Schema right)
    {
        if (RequireAll(left, j.Keys.Select(k => k.Left)) is { } l) return l;
        if (RequireAll(right, j.Keys.Select(k => k.Right)) is { } r) return r;
        foreach (var key in j.Keys)
        {
            var (a, b) = (left.Find(key.Left)!.Value.Type, right.Find(key.Right)!.Value.Type);
            if (!Comparable(a, b))
                return SchemaResult.Fail($"Join keys '{key.Left}' ({a}) and '{key.Right}' ({b}) have incompatible types.");
        }
        if (j.Kind is JoinKind.Semi or JoinKind.Anti) return SchemaResult.Of(left);
        var rightColumns = right.Columns.Select(c => left.Has(c.Name) ? c with { Name = c.Name + RightSuffix } : c);
        return SchemaResult.Of(new Schema([.. left.Columns, .. rightColumns]));
    }

    private static SchemaResult InferUnion(IReadOnlyList<Schema> inputs)
    {
        if (inputs.Count == 0) return SchemaResult.Fail("Union needs at least one input.");
        var first = inputs[0];
        for (var i = 1; i < inputs.Count; i++)
        {
            if (inputs[i].Columns.Count != first.Columns.Count)
                return SchemaResult.Fail($"Union input {i + 1} has {inputs[i].Columns.Count} columns, expected {first.Columns.Count}.");
            for (var c = 0; c < first.Columns.Count; c++)
                if (!Comparable(first.Columns[c].Type, inputs[i].Columns[c].Type))
                    return SchemaResult.Fail($"Union column '{first.Columns[c].Name}' is {first.Columns[c].Type} in input 1 but {inputs[i].Columns[c].Type} in input {i + 1}.");
        }
        return SchemaResult.Of(first);
    }

    private static SchemaResult InferAggregate(Aggregate a, Schema input)
    {
        if (RequireAll(input, a.GroupBy) is { } missing) return missing;
        if (RequireAll(input, a.Aggregates.Where(x => x.Column is not null).Select(x => x.Column!)) is { } missingAgg) return missingAgg;
        var columns = a.GroupBy.Select(g => input.Find(g)!.Value).ToList();
        foreach (var agg in a.Aggregates)
        {
            var source = agg.Column is null ? (Column?)null : input.Find(agg.Column);
            var type = AggregateType(agg.Function, source?.Type);
            if (type is null)
                return SchemaResult.Fail($"Cannot apply {agg.Function} to '{agg.Column}' of type {source?.Type}.");
            columns.Add(new Column(agg.Name, type.Value, Nullable: agg.Function != AggregateFunction.Count));
        }
        var clash = columns.GroupBy(c => c.Name, StringComparer.OrdinalIgnoreCase).FirstOrDefault(g => g.Count() > 1);
        return clash is null ? SchemaResult.Of(new Schema(columns)) : SchemaResult.Fail($"Aggregate produces duplicate column '{clash.Key}'.");
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

    private static bool Comparable(ColumnType a, ColumnType b)
        => a == b || TypeChecker.Unify(a.ToScalarType(), b.ToScalarType()).Ok && a.ToScalarType() is not null && b.ToScalarType() is not null;

    private static SchemaResult Require(Schema schema, string column, Func<Column, SchemaResult> then)
        => schema.Find(column) is { } c ? then(c) : SchemaResult.Fail($"No column named '{column}'.");

    /// <summary>Null when every column exists, else the failure naming the first missing one.</summary>
    private static SchemaResult? RequireAll(Schema schema, IEnumerable<string> columns)
        => columns.FirstOrDefault(c => !schema.Has(c)) is { } missing ? SchemaResult.Fail($"No column named '{missing}'.") : null;
}
