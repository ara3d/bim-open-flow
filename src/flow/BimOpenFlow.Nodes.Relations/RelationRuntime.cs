using Ara3D.DataFlowEngine;
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
    private readonly InlineTableStore _inlines;
    private readonly object _gate = new();

    public RelationRuntime(IConnectionRegistry registry, ICatalog? catalog = null, int resultCapacity = 64, int inlineCapacity = 16)
    {
        Registry = registry;
        Catalog = catalog ?? new DuckDbCatalog(registry);
        _schemas = new SchemaCache(Catalog);
        _results = new ResultCache(resultCapacity);
        _inlines = new InlineTableStore(inlineCapacity);
    }

    /// <summary>A runtime whose sources are the given roots, rescanned on every lookup so a
    /// database written into a root later resolves without a restart.</summary>
    public static RelationRuntime FromRoots(IReadOnlyList<string> roots)
        => new(new RootScanRegistry(roots));

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
            () => Compile(plan).Execute(Registry, limit, offset, inlines: _inlines).Conforming(Schema(plan).Require()));

    public long Count(Plan plan)
        => Compile(plan).Count(Registry, _inlines);

    /// <summary>Registers an in-process table under its content hash and returns the plan node
    /// that reads it. The same rows under the same name give the same plan, so a graph that
    /// re-evaluates an unchanged table hits the schema and result caches.</summary>
    public InlineTable Inline(IDataTable table, string name)
    {
        var hash = ValueHash.Compute(new TableValue(table));
        _inlines.Add(hash, table);
        return new InlineTable(name, hash, SchemaOf(table));
    }

    /// <summary>An inline table types itself from the CLR types its columns carry, using the
    /// same mapping the executor checks materialized results against.</summary>
    private static Schema SchemaOf(IDataTable table)
        => new(table.Columns.Select(c => new Column(c.Descriptor.Name, DuckDbTypes.FromClr(c.Descriptor.Type))).ToList());

    /// <summary>Forget inferred schemas and materialized rows, for when a source changed on disk.</summary>
    public void Invalidate()
    {
        lock (_gate) _schemas.Clear();
        _results.Clear();
    }
}
