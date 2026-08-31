using Ara3D.DataFlowEngine.Abstractions;
using BimOpenFlow.Host.Api;
using BimOpenFlow.Host.Catalog;
using BimOpenFlow.Host.Store;
using BimOpenFlow.Nodes.Bos;
using BimOpenFlow.Nodes.Compliance;
using BimOpenFlow.Nodes.Effects;
using BimOpenFlow.Nodes.Geometry;

namespace BimOpenFlow.Host;

/// <summary>The wired core: everything the host (or another front end, e.g. the
/// MCP server) needs, with no HTTP attached.</summary>
public sealed record HostServices(ModelCatalog Catalog, AnalysisStore Store, NodeRegistry Registry);

/// <summary>The full host: services plus the composed HTTP application.</summary>
public sealed record HostApp(HostConfig Config, HostServices Services, WebApplication App);

/// <summary>The composition root. Wiring only; any logic belongs in the modules.</summary>
public static class HostComposition
{
    /// <summary>The one real node registry: all four packs combined.</summary>
    public static NodeRegistry AllPacks()
        => NodeRegistry.Combine(BosNodes.All, GeometryNodes.All, ComplianceNodes.All, EffectNodes.All);

    public static HostServices BuildServices(HostConfig config)
        => new(
            new ModelCatalog(config.ModelRoots, config.CacheDir),
            new AnalysisStore(config.StoreDir),
            AllPacks());

    public static HostApp Build(HostConfig config)
    {
        var services = BuildServices(config);
        var app = ApiServer.Create(services.Catalog, services.Store, services.Registry);
        app.MapModelBytes(services.Catalog);
        app.Urls.Add($"http://127.0.0.1:{config.Port}");
        return new(config, services, app);
    }
}
