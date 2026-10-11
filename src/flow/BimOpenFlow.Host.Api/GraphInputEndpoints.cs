using Ara3D.DataFlowEngine.Abstractions;
using BimOpenFlow.Contracts;
using BimOpenFlow.Host.Store;

namespace BimOpenFlow.Host.Api;

/// <summary>An analysis's graph inputs: their declarations and current values, and a change of one value.</summary>
internal static class GraphInputEndpoints
{
    public static void MapGraphInputEndpoints(this IEndpointRouteBuilder app, AnalysisStore store, AnalysisSessions sessions)
    {
        app.MapGet(ApiRoutes.GetGraphInputs, (string id) => ApiResults.Guard(() =>
            store.Exists(id)
                ? ApiResults.Json(sessions.Snapshot(id).ToGraphInputInfos())
                : ApiResults.NotFound($"Analysis '{id}' not found")));

        app.MapPut(ApiRoutes.PutGraphInput, async (HttpContext context, string id, string name) =>
        {
            using var reader = new StreamReader(context.Request.Body);
            var value = await reader.ReadToEndAsync(context.RequestAborted);
            return ApiResults.Guard(() =>
                store.Exists(id)
                    ? ApiResults.Json(sessions.SetGraphInputs(id, new Dictionary<string, string> { [name] = value })
                        .ToGraphInputInfos())
                    : ApiResults.NotFound($"Analysis '{id}' not found"));
        });
    }
}
