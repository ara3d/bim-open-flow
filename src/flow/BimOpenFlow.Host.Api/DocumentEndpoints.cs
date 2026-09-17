using Ara3D.DataFlowEngine.Abstractions;
using Ara3D.NodeGraph;
using BimOpenFlow.Contracts;
using BimOpenFlow.Host.Catalog;
using BimOpenFlow.Host.Store;
using AnalysisVersion = BimOpenFlow.Contracts.AnalysisVersion;

namespace BimOpenFlow.Host.Api;

/// <summary>Models, analysis documents (list/get/put/history), and the node catalog.</summary>
internal static class DocumentEndpoints
{
    public static void MapDocumentEndpoints(this IEndpointRouteBuilder app,
        ModelCatalog catalog, AnalysisStore store, INodeRegistry registry, AnalysisSessions sessions)
    {
        app.MapGet(ApiRoutes.ListModels, () => ApiResults.Guard(() =>
            ApiResults.Json(catalog.Scan().Select(e => e.ToSummary()).ToList())));

        app.MapGet(ApiRoutes.ListAnalyses, () => ApiResults.Guard(() =>
            ApiResults.Json(store.List()
                .Select(a => new AnalysisSummary(a.Id, store.Load(a.Id).ComputeGraphHash()))
                .ToList())));

        app.MapGet(ApiRoutes.GetAnalysis, (string id) => ApiResults.Guard(() =>
            store.Exists(id)
                ? Results.Text(store.Load(id).ToCanonicalJson(), "application/json")
                : ApiResults.NotFound($"Analysis '{id}' not found")));

        app.MapPut(ApiRoutes.PutAnalysis, async (HttpContext context, string id) =>
        {
            using var reader = new StreamReader(context.Request.Body);
            var text = await reader.ReadToEndAsync(context.RequestAborted);
            return ApiResults.Guard(() => PutAnalysis(store, registry, sessions, id, text));
        });

        app.MapGet(ApiRoutes.GetAnalysisHistory, (string id) => ApiResults.Guard(() =>
            store.Exists(id)
                ? ApiResults.Json(store.History(id)
                    .Select(v => new AnalysisVersion(v.Sequence, v.GraphHash))
                    .ToList())
                : ApiResults.NotFound($"Analysis '{id}' not found")));

        app.MapGet(ApiRoutes.GetNodeCatalog, () => ApiResults.Guard(() =>
            ApiResults.Json(registry.ToCatalog())));
    }

    private static IResult PutAnalysis(AnalysisStore store, INodeRegistry registry,
        AnalysisSessions sessions, string id, string text)
    {
        var doc = GraphDocumentIO.Parse(text);
        var errors = doc.Validate(registry);
        if (errors.Count > 0)
            return ApiResults.BadRequest(string.Join("; ", errors.Select(e => e.Message)));
        store.Save(id, doc);
        sessions.Set(id, doc);
        return ApiResults.Json(new AnalysisSummary(id, doc.ComputeGraphHash()));
    }
}
