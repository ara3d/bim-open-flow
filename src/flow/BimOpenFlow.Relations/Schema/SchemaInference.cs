namespace BimOpenFlow.Relations;

/// <summary>Walks a plan and applies <see cref="SchemaRules"/> to each operator. Errors are values; the
/// first failing input stops inference for everything above it.</summary>
public static class SchemaInference
{
    public const string RightSuffix = "_right";

    public static SchemaResult Infer(this Plan plan, ISchemaCatalog catalog)
        => new SchemaCache(catalog).Infer(plan);

    internal static SchemaResult InferNode(Plan plan, IReadOnlyList<SchemaResult> inputs, ISchemaCatalog catalog)
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
            Select s => SchemaRules.Select(schemas[0], s.Columns),
            Rename r => SchemaRules.Rename(schemas[0], r.Renamings),
            Cast c => SchemaRules.Cast(schemas[0], c.Column, c.Type),
            Derive d => SchemaRules.Derive(schemas[0], d.Name, d.Expression),
            Filter f => SchemaRules.Filter(schemas[0], f.Predicate),
            Distinct => SchemaRules.Distinct(schemas[0], []),
            Sort s => SchemaRules.Sort(schemas[0], s.Keys.Select(k => k.Column)),
            Limit => SchemaResult.Of(schemas[0]),
            Join j => SchemaRules.Join(schemas[0], schemas[1], j.Keys, j.Kind),
            Union => SchemaRules.Union(schemas),
            Aggregate a => SchemaRules.Aggregate(schemas[0], a.GroupBy, a.Aggregates),
            _ => SchemaResult.Fail($"No inference rule for {plan.GetType().Name}."),
        };
    }
}
