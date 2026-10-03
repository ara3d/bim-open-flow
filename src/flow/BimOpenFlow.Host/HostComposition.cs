using Ara3D.DataFlowEngine.Abstractions;
using BimOpenFlow.Host.Api;
using BimOpenFlow.Host.Catalog;
using BimOpenFlow.Host.Store;
using BimOpenFlow.Nodes.Cleaning;
using BimOpenFlow.Nodes.Dates;
using BimOpenFlow.Nodes.DuckDb;
using BimOpenFlow.Nodes.Effects;
using BimOpenFlow.Nodes.Relations;
using BimOpenFlow.Nodes.Spatial;
using BimOpenFlow.Nodes.TableOps;
using BimOpenFlow.Nodes.Tables;
using BimOpenFlow.Nodes.Viz;
using BimOpenFlow.Relations.DuckDb;

namespace BimOpenFlow.Host;

/// <summary>The wired core: everything the host (or another front end, e.g. the
/// MCP server) needs, with no HTTP attached.</summary>
public sealed record HostServices(ModelCatalog Catalog, AnalysisStore Store, NodeRegistry Registry, RelationRuntime Relations, AnalysisSessions Sessions, EditorSessions Editor);

/// <summary>The full host: services plus the composed HTTP application.</summary>
public sealed record HostApp(HostConfig Config, HostServices Services, WebApplication App);

/// <summary>The composition root. Wiring only; any logic belongs in the modules.</summary>
public static class HostComposition
{
    /// <summary>The "tables" profile registry: the DuckDB, Tables, TableOps, Cleaning,
    /// Dates, Viz, and Spatial packs, the table writers from the Effects pack, plus the
    /// rel.* pack. Nothing here references BIM Open Schema.</summary>
    public static NodeRegistry TablePacks(RelationRuntime? relations = null)
        => NodeRegistry.Combine(DuckDbNodes.All, TableNodes.All,
            TableOpsNodes.All, CleaningNodes.All, DatesNodes.All, VizNodes.All, SpatialNodes.All,
            EffectNodes.TableSinks, RelationNodes.All(relations ?? NoSources()));

    /// <summary>The generic "tables" profile: the table packs, seeded with samples/analyses and
    /// samples/relations over samples/tables, with nothing to prepare in the background.</summary>
    public static readonly HostProfile Tables = new(HostConfig.TablesProfile, TablePacks,
        SampleSeeding.SeededModelRoots, SampleSeeding.Seed, _ => []);

    /// <summary>The profiles bimopenflow-host offers on its own: only "tables". The studio
    /// composes the "bim" profile (src/studio/BimOpenFlow.Studio/StudioComposition.cs).</summary>
    public static readonly HostProfiles Generic = new(HostConfig.TablesProfile, [Tables]);

    /// <summary>A relation runtime with no sources: rel.csv and rel.table resolve nothing.</summary>
    public static RelationRuntime NoSources()
        => RelationRuntime.FromRoots([]);

    /// <summary>The profile's registry over a relation runtime whose sources are the model
    /// roots, rescanned on use: each root folder by name for CSV files, each .duckdb file
    /// inside one by file name. Sources a preparation job is still building answer
    /// "not ready yet" instead of "unknown".</summary>
    public static HostServices BuildServices(HostConfig config, HostProfile profile,
        IReadOnlyList<SamplePreparation.Job>? preparing = null)
    {
        var relations = new RelationRuntime(new PreparingRegistry(
            new RootScanRegistry(config.ModelRoots), SamplePreparation.PendingReason(preparing ?? [])));
        var store = new AnalysisStore(config.StoreDir);
        var registry = profile.Packs(relations);
        return new(
            new ModelCatalog(config.ModelRoots, config.CacheDir),
            store,
            registry,
            relations,
            new AnalysisSessions(store, registry),
            new EditorSessions(store));
    }

    public static HostApp Build(HostConfig config, HostProfile profile,
        IReadOnlyList<SamplePreparation.Job>? preparing = null)
    {
        var services = BuildServices(config, profile, preparing);
        var app = ApiServer.Create(services.Catalog, services.Store, services.Registry,
            DuckDbTableProbe.Tables, relations: new RelationHostResults(services.Relations), sessions: services.Sessions,
            editor: services.Editor);
        app.Urls.Add($"http://127.0.0.1:{config.Port}");
        return new(config, services, app);
    }
}
