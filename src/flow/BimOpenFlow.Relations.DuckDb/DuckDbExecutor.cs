using Ara3D.BimOpenSchema.DuckDb;
using Ara3D.DataTable;

namespace BimOpenFlow.Relations.DuckDb;

/// <summary>Runs compiled SQL against bound sources. Sees SQL and a registry, never a plan,
/// which is what keeps the executor replaceable.</summary>
public static class DuckDbExecutor
{
    public static IDataTable Execute(this CompiledQuery query, IConnectionRegistry registry, long? limit = null,
        long offset = 0, string name = "result", IInlineTables? inlines = null)
    {
        using var session = DuckDbSession.For(query.Sources, registry, query.Inlines, inlines ?? NoInlineTables.Instance);
        var sql = limit is { } n ? query.WithLimit(n, offset) : query.Sql;
        return session.Connection.Query(sql, name).NormalizeDatesToText();
    }

    public static long Count(this CompiledQuery query, IConnectionRegistry registry, IInlineTables? inlines = null)
    {
        using var session = DuckDbSession.For(query.Sources, registry, query.Inlines, inlines ?? NoInlineTables.Instance);
        return session.Connection.ScalarInt64(query.CountSql);
    }

    /// <summary>Compile, run, and check the rows against the inferred schema in one call.</summary>
    public static IDataTable Execute(this Plan plan, ICatalog catalog, IConnectionRegistry registry,
        long? limit = null, long offset = 0, IInlineTables? inlines = null)
    {
        var schemas = new SchemaCache(catalog);
        var table = plan.Compile(schemas).Execute(registry, limit, offset, inlines: inlines);
        return table.Conforming(schemas.Infer(plan).Require());
    }

    /// <summary>The table unchanged, or an exception naming the first column whose name or
    /// type differs from the declared schema. Sources that changed since design time show up here.</summary>
    public static IDataTable Conforming(this IDataTable table, Schema schema)
    {
        if (table.Columns.Count != schema.Columns.Count)
            throw new InvalidOperationException($"Expected {schema.Columns.Count} columns but the result has {table.Columns.Count}.");
        for (var i = 0; i < schema.Columns.Count; i++)
        {
            var (expected, actual) = (schema.Columns[i], table.Columns[i].Descriptor);
            if (!string.Equals(expected.Name, actual.Name, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException($"Expected column {i + 1} to be '{expected.Name}' but the result has '{actual.Name}'.");
            if (!DuckDbTypes.Carries(expected.Type, actual.Type))
                throw new InvalidOperationException($"Column '{expected.Name}' was inferred as {expected.Type} but the result holds {actual.Type.Name}.");
        }
        return table;
    }
}
