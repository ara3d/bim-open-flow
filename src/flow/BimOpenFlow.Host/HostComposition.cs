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

namespace BimOpenFlow.Host;

/// <summary>The wired core: everything the host (or another front end, e.g. the
/// MCP server) needs, with no HTTP attached.</summary>
public sealed record HostServices(ModelCatalog Catalog, AnalysisStore Store, NodeRegistry Registry, RelationRuntime Relations);

/// <summary>The full host: services plus the composed HTTP application.</summary>
public sealed record HostApp(HostConfig Config, HostServices Services, WebApplication App);

/// <summary>The composition root. Wiring only; any logic belongs in the modules.</summary>
public static class HostComposition
{
    /// <summary>The "bim" profile registry: all five BIM packs plus the Viz pack.</summary>
    public static NodeRegistry AllPacks()
        => NodeRegistry.Combine(BosNodes.All, BimAnalysisNodes.All, GeometryNodes.All,
            ComplianceNodes.All, EffectNodes.All, VizNodes.All);

    /// <summary>The "tables" profile registry: the DuckDB, Tables, TableOps,
    /// Cleaning, Dates, and Viz packs, the table writers from the Effects pack,
    /// plus the four BIM-free table.* nodes cherry-picked from the Bos pack.</summary>
    public static NodeRegistry TablePacks()
        => NodeRegistry.Combine(DuckDbNodes.All, TableNodes.All,
            TableOpsNodes.All, CleaningNodes.All, DatesNodes.All, VizNodes.All, EffectNodes.TableSinks,
            [new TableFilterNode(), new TableDeriveNode(), new TableAggregateNode(), new TableSortNode()]);

    /// <summary>Either profile plus the rel.* pack, whose sources are the model roots:
    /// each root folder by name for CSV files, each .duckdb file inside one by file name.</summary>
    public static HostServices BuildServices(HostConfig config)
    {
        var relations = RelationRuntime.FromRoots(config.ModelRoots);
        var packs = config.Profile == HostConfig.TablesProfile ? TablePacks() : AllPacks();
        return new(
            new ModelCatalog(config.ModelRoots, config.CacheDir),
            new AnalysisStore(config.StoreDir),
            NodeRegistry.Combine(packs.Nodes, RelationNodes.All(relations)),
            relations);
    }

    public static HostApp Build(HostConfig config)
    {
        var services = BuildServices(config);
        var app = ApiServer.Create(services.Catalog, services.Store, services.Registry,
            DuckDbTableProbe.Tables, relations: new RelationHostResults(services.Relations));
        app.Urls.Add($"http://127.0.0.1:{config.Port}");
        return new(config, services, app);
    }
}
