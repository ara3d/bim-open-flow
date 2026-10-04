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
        IRelationResults? relations = null, AnalysisSessions? sessions = null, EditorSessions? editor = null)
    {
        var builder = WebApplication.CreateBuilder(args ?? Array.Empty<string>());
        var app = builder.Build();
        app.MapBimOpenFlowApi(catalog, store, registry, fileTables, relations, sessions, editor);
        return app;
    }

    /// <summary>Maps every route. The caller may pass the sessions so it can reach them
    /// after start-up (to re-evaluate when background data lands); otherwise they are private.</summary>
    public static IEndpointRouteBuilder MapBimOpenFlowApi(this IEndpointRouteBuilder app,
        ModelCatalog catalog, AnalysisStore store, INodeRegistry registry,
        FileTableProbe? fileTables = null, IRelationResults? relations = null, AnalysisSessions? sessions = null,
        EditorSessions? editor = null)
    {
        sessions ??= new AnalysisSessions(store, registry);
        editor ??= new EditorSessions(store);
        app.MapDocumentEndpoints(catalog, store, registry, sessions);
        app.MapModelBytes(catalog);
        app.MapEntityProperties(catalog);
        // The composition's IRelationResults doubles as a GraphText.IRelationReader when it
        // knows how to execute relations (RelationHostResults); the text endpoint uses it to
        // print a relation's row count and rows instead of its plan alone.
        app.MapEvalEndpoints(catalog, store, registry, sessions, relations, relations as BimOpenFlow.GraphText.IRelationReader);
        app.MapSuggestEndpoints(store, registry, sessions, fileTables, relations);
        app.MapSessionEndpoints(editor);
        return app;
    }
}
