using Ara3D.DataFlowEngine.Abstractions;
using BimOpenFlow.Host.Api;
using BimOpenFlow.Host.Catalog;
using BimOpenFlow.Host.Store;
using BimOpenFlow.Nodes.BimAnalysis;
using BimOpenFlow.Nodes.Bos;
using BimOpenFlow.Nodes.Cleaning;
using BimOpenFlow.Nodes.Compliance;
using BimOpenFlow.Nodes.Dates;
using BimOpenFlow.Nodes.DuckDb;
using BimOpenFlow.Nodes.Effects;
using BimOpenFlow.Nodes.Geometry;
using BimOpenFlow.Nodes.Relations;
using BimOpenFlow.Nodes.TableOps;
using BimOpenFlow.Nodes.Tables;
using BimOpenFlow.Nodes.Viz;
using BimOpenFlow.Relations.DuckDb;

namespace BimOpenFlow.Host;

/// <summary>The wired core: everything the host (or another front end, e.g. the
/// MCP server) needs, with no HTTP attached.</summary>
public sealed record HostServices(ModelCatalog Catalog, AnalysisStore Store, NodeRegistry Registry, RelationRuntime Relations, AnalysisSessions Sessions);

/// <summary>The full host: services plus the composed HTTP application.</summary>
public sealed record HostApp(HostConfig Config, HostServices Services, WebApplication App);

/// <summary>The composition root. Wiring only; any logic belongs in the modules.</summary>
public static class HostComposition
{
    /// <summary>The "bim" profile registry: the Bos, BimAnalysis, Geometry, Compliance,
    /// Effects, and Viz packs plus the rel.* pack. Without a runtime the rel.* pack sees no
    /// sources, which is enough for validation, catalogs, and docs.</summary>
    public static NodeRegistry AllPacks(RelationRuntime? relations = null)
        => NodeRegistry.Combine(BosNodes.All, BimAnalysisNodes.All, GeometryNodes.All,
            ComplianceNodes.All, EffectNodes.All, VizNodes.All, RelationNodes.All(relations ?? NoSources()));

    /// <summary>The "tables" profile registry: the DuckDB, Tables, TableOps, Cleaning,
    /// Dates, and Viz packs, the table writers from the Effects pack, the four BIM-free
    /// table.* nodes cherry-picked from the Bos pack, plus the rel.* pack.</summary>
    public static NodeRegistry TablePacks(RelationRuntime? relations = null)
        => NodeRegistry.Combine(DuckDbNodes.All, TableNodes.All,
            TableOpsNodes.All, CleaningNodes.All, DatesNodes.All, VizNodes.All, EffectNodes.TableSinks,
            [new TableFilterNode(), new TableDeriveNode(), new TableAggregateNode(), new TableSortNode()],
            RelationNodes.All(relations ?? NoSources()));

    /// <summary>The registry a profile name selects, over the given relation sources.</summary>
    public static NodeRegistry Registry(string profile, RelationRuntime relations)
        => profile == HostConfig.TablesProfile ? TablePacks(relations) : AllPacks(relations);

    /// <summary>A relation runtime with no sources: rel.csv and rel.table resolve nothing.</summary>
    public static RelationRuntime NoSources()
        => RelationRuntime.FromRoots([]);

    /// <summary>The profile's registry over a relation runtime whose sources are the model
    /// roots, rescanned on use: each root folder by name for CSV files, each .duckdb file
    /// inside one by file name. Sources a preparation job is still building answer
    /// "not ready yet" instead of "unknown".</summary>
    public static HostServices BuildServices(HostConfig config, IReadOnlyList<SamplePreparation.Job>? preparing = null)
    {
        var relations = new RelationRuntime(new PreparingRegistry(
            new RootScanRegistry(config.ModelRoots), SamplePreparation.PendingReason(preparing ?? [])));
        var store = new AnalysisStore(config.StoreDir);
        var registry = Registry(config.Profile, relations);
        return new(
            new ModelCatalog(config.ModelRoots, config.CacheDir),
            store,
            registry,
            relations,
            new AnalysisSessions(store, registry));
    }

    public static HostApp Build(HostConfig config, IReadOnlyList<SamplePreparation.Job>? preparing = null)
    {
        var services = BuildServices(config, preparing);
        var app = ApiServer.Create(services.Catalog, services.Store, services.Registry,
            DuckDbTableProbe.Tables, relations: new RelationHostResults(services.Relations), sessions: services.Sessions);
        app.Urls.Add($"http://127.0.0.1:{config.Port}");
        return new(config, services, app);
    }
}
