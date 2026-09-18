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
        INodeRegistry registry, FileTableProbe? fileTables = null, string[]? args = null,
        IRelationResults? relations = null, AnalysisSessions? sessions = null)
    {
        var builder = WebApplication.CreateBuilder(args ?? Array.Empty<string>());
        var app = builder.Build();
        app.MapBimOpenFlowApi(catalog, store, registry, fileTables, relations, sessions);
        return app;
    }

    /// <summary>Maps every route. The caller may pass the sessions so it can reach them
    /// after start-up (to re-evaluate when background data lands); otherwise they are private.</summary>
    public static IEndpointRouteBuilder MapBimOpenFlowApi(this IEndpointRouteBuilder app,
        ModelCatalog catalog, AnalysisStore store, INodeRegistry registry,
        FileTableProbe? fileTables = null, IRelationResults? relations = null, AnalysisSessions? sessions = null)
    {
        sessions ??= new AnalysisSessions(store, registry);
        app.MapDocumentEndpoints(catalog, store, registry, sessions);
        app.MapModelBytes(catalog);
        app.MapEvalEndpoints(catalog, store, registry, sessions, relations);
        app.MapSuggestEndpoints(store, registry, sessions, fileTables, relations);
        return app;
    }
}
