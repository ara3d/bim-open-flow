using Ara3D.DataFlowEngine.Abstractions;
using BimOpenFlow.Host.Catalog;
using BimOpenFlow.Host.Store;

namespace BimOpenFlow.Host.Api;

/// <summary>
/// Composes the BimOpenFlow HTTP surface (minimal APIs on the generated
/// ApiRoutes templates). The host calls Create and runs the returned app;
/// tests start it on an ephemeral port.
/// </summary>
public static class ApiServer
{
    public static WebApplication Create(ModelCatalog catalog, AnalysisStore store,
        INodeRegistry registry, string[]? args = null)
    {
        var builder = WebApplication.CreateBuilder(args ?? Array.Empty<string>());
        var app = builder.Build();
        app.MapBimOpenFlowApi(catalog, store, registry);
        return app;
    }

    public static IEndpointRouteBuilder MapBimOpenFlowApi(this IEndpointRouteBuilder app,
        ModelCatalog catalog, AnalysisStore store, INodeRegistry registry)
    {
        var sessions = new AnalysisSessions(store, registry);
        app.MapDocumentEndpoints(catalog, store, registry, sessions);
        app.MapEvalEndpoints(catalog, store, registry, sessions);
        return app;
    }
}
