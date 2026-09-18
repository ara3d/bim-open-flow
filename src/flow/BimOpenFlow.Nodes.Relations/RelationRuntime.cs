using Ara3D.DataTable;
using BimOpenFlow.Relations.DuckDb;

namespace BimOpenFlow.Nodes.Relations;

/// <summary>What the rel.* nodes and the host share: the registry that names sources, the
/// catalog that types them, the schema cache, and the bounded result cache. Schema
/// inference is serialized on one lock; materialization runs outside it.</summary>
public sealed class RelationRuntime
{
    public IConnectionRegistry Registry { get; }
    public ICatalog Catalog { get; }
    private readonly SchemaCache _schemas;
    private readonly ResultCache _results;
    private readonly object _gate = new();

    public RelationRuntime(IConnectionRegistry registry, ICatalog? catalog = null, int resultCapacity = 64)
    {
        Registry = registry;
        Catalog = catalog ?? new DuckDbCatalog(registry);
        _schemas = new SchemaCache(Catalog);
        _results = new ResultCache(resultCapacity);
    }

    public static RelationRuntime FromRoots(IReadOnlyList<string> roots)
        => new(SourceRegistries.FromRoots(roots));

    public SchemaResult Schema(Plan plan)
    {
        lock (_gate) return _schemas.Infer(plan);
    }

    /// <summary>The plan as a wire value, or an ArgumentException naming the node and the schema error.</summary>
    public RelationValue Relation(Plan plan, string kind)
    {
        var schema = Schema(plan);
        return schema.Ok
            ? new RelationValue(plan.Text, plan.Hash, plan)
            : throw new ArgumentException($"{kind}: {schema}");
    }

    public CompiledQuery Compile(Plan plan)
    {
        lock (_gate) return plan.Compile(_schemas);
    }

    public IDataTable Materialize(Plan plan, long? limit = null, long offset = 0)
        => _results.GetOrAdd(plan, limit, offset,
            () => Compile(plan).Execute(Registry, limit, offset).Conforming(Schema(plan).Require()));

    public long Count(Plan plan)
        => Compile(plan).Count(Registry);

    /// <summary>Registers an in-process table under its content hash and returns the plan node
    /// that reads it. Contract C4 of the nrc-handoff wave; the body belongs to track C.</summary>
    public InlineTable Inline(IDataTable table, string name)
        => throw new NotImplementedException("Track C fills in RelationRuntime.Inline.");

    /// <summary>Forget inferred schemas and materialized rows, for when a source changed on disk.</summary>
    public void Invalidate()
    {
        lock (_gate) _schemas.Clear();
        _results.Clear();
    }
}
