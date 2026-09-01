using Ara3D.DataFlowEngine.Abstractions;
using BimOpenFlow.Host.Api;
using BimOpenFlow.Host.Catalog;
using BimOpenFlow.Host.Store;
using BimOpenFlow.Nodes.Bos;
using BimOpenFlow.Nodes.Cleaning;
using BimOpenFlow.Nodes.Compliance;
using BimOpenFlow.Nodes.Dates;
using BimOpenFlow.Nodes.DuckDb;
using BimOpenFlow.Nodes.Effects;
using BimOpenFlow.Nodes.Geometry;
using BimOpenFlow.Nodes.TableOps;
using BimOpenFlow.Nodes.Tables;

namespace BimOpenFlow.Host;

/// <summary>The wired core: everything the host (or another front end, e.g. the
/// MCP server) needs, with no HTTP attached.</summary>
public sealed record HostServices(ModelCatalog Catalog, AnalysisStore Store, NodeRegistry Registry);

/// <summary>The full host: services plus the composed HTTP application.</summary>
public sealed record HostApp(HostConfig Config, HostServices Services, WebApplication App);

/// <summary>The composition root. Wiring only; any logic belongs in the modules.</summary>
public static class HostComposition
{
    /// <summary>The "bim" profile registry: all four BIM packs combined.</summary>
    public static NodeRegistry AllPacks()
        => NodeRegistry.Combine(BosNodes.All, GeometryNodes.All, ComplianceNodes.All, EffectNodes.All);

    /// <summary>The "tables" profile registry: the DuckDB, Tables, TableOps,
    /// Cleaning, and Dates packs, the table writers from the Effects pack, plus
    /// the four BIM-free table.* nodes cherry-picked from the Bos pack.</summary>
    public static NodeRegistry TablePacks()
        => NodeRegistry.Combine(DuckDbNodes.All, TableNodes.All,
            TableOpsNodes.All, CleaningNodes.All, DatesNodes.All, EffectNodes.TableSinks,
            [new TableFilterNode(), new TableDeriveNode(), new TableAggregateNode(), new TableSortNode()]);

    public static HostServices BuildServices(HostConfig config)
        => new(
            new ModelCatalog(config.ModelRoots, config.CacheDir),
            new AnalysisStore(config.StoreDir),
            config.Profile == HostConfig.TablesProfile ? TablePacks() : AllPacks());

    public static HostApp Build(HostConfig config)
    {
        var services = BuildServices(config);
        var app = ApiServer.Create(services.Catalog, services.Store, services.Registry);
        app.MapModelBytes(services.Catalog);
        app.Urls.Add($"http://127.0.0.1:{config.Port}");
        return new(config, services, app);
    }
}
