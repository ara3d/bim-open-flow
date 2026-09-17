using System.Threading.Channels;
using Ara3D.DataFlowEngine;
using Ara3D.DataFlowEngine.Abstractions;
using Ara3D.DataFlowEngine.Runs;
using BimOpenFlow.Contracts;
using BimOpenFlow.Host.Catalog;
using BimOpenFlow.Host.Store;
using EngineNodeStatus = Ara3D.DataFlowEngine.NodeStatus;
using RunSummary = BimOpenFlow.Contracts.RunSummary;

namespace BimOpenFlow.Host.Api;

/// <summary>Evaluation state, result paging, run archival, and the SSE event stream.</summary>
internal static class EvalEndpoints
{
    public const int DefaultTake = 1000;
    public static readonly TimeSpan KeepAliveInterval = TimeSpan.FromSeconds(15);

    public static readonly string EngineVersion =
        typeof(EvalSession).Assembly.GetName().Version?.ToString(3) ?? "0.0.0";

    public static void MapEvalEndpoints(this IEndpointRouteBuilder app,
        ModelCatalog catalog, AnalysisStore store, INodeRegistry registry, AnalysisSessions sessions)
    {
        app.MapGet(ApiRoutes.GetAnalysisState, (string id) => ApiResults.Guard(() =>
            store.Exists(id)
                ? ApiResults.Json(sessions.Snapshot(id).ToEvalUpdate(id))
                : ApiResults.NotFound($"Analysis '{id}' not found")));

        app.MapGet(ApiRoutes.GetResult, (string id, string nodeId, string port, int? skip, int? take)
            => ApiResults.Guard(() =>
                GetResult(store, registry, sessions, id, nodeId, port, skip ?? 0, take ?? DefaultTake)));

        app.MapGet(ApiRoutes.ListRuns, (string id) => ApiResults.Guard(() =>
            store.Exists(id)
                ? ApiResults.Json(store.ListRuns(id).Select(f => ToSummary(store, id, f)).ToList())
                : ApiResults.NotFound($"Analysis '{id}' not found")));

        app.MapPost(ApiRoutes.CreateRun, (string id) => ApiResults.Guard(() =>
            CreateRun(catalog, store, registry, sessions, id)));

        app.MapGet(ApiRoutes.GetRun, (string id, string fileName) => ApiResults.Guard(() =>
            !store.Exists(id)
                ? ApiResults.NotFound($"Analysis '{id}' not found")
                : fileName.Contains('/') || fileName.Contains('\\') || fileName.Contains("..")
                    ? ApiResults.BadRequest($"Invalid run file name '{fileName}'")
                    : Results.Text(store.LoadRun(id, fileName).ToCanonicalJson(), "application/json")));

        app.MapGet(ApiRoutes.AnalysisEvents, (HttpContext context, string id)
            => StreamEvents(context, store, sessions, id));
    }

    private static IResult GetResult(AnalysisStore store, INodeRegistry registry,
        AnalysisSessions sessions, string id, string nodeId, string port, int skip, int take)
    {
        if (!store.Exists(id))
            return ApiResults.NotFound($"Analysis '{id}' not found");
        var snapshot = sessions.Snapshot(id);
        var node = snapshot.Document.FindNode(nodeId);
        if (node is null)
            return ApiResults.NotFound($"Node '{nodeId}' not in analysis '{id}'");
        var spec = registry.Find(node.Kind, node.Version)!.Spec;
        var index = IndexOfOutput(spec, port);
        if (index < 0)
            return ApiResults.NotFound($"Node '{nodeId}' has no output port '{port}'");
        var result = snapshot.Results.GetValueOrDefault(nodeId);
        if (result is null || result.Status != EngineNodeStatus.Ok)
            return ApiResults.NotFound(
                $"No result for '{nodeId}.{port}' (status {result?.Status.ToString() ?? "unknown"})");
        return ApiResults.Json(result.Outputs[index].ToSlice(port, skip, take));
    }

    private static int IndexOfOutput(NodeSpec spec, string port)
    {
        for (var i = 0; i < spec.Outputs.Count; i++)
            if (spec.Outputs[i].Name == port)
                return i;
        return -1;
    }

    private static RunSummary ToSummary(AnalysisStore store, string id, string fileName)
    {
        var record = store.LoadRun(id, fileName);
        return new(fileName, record.TimestampUtc, record.GraphHash);
    }

    private static IResult CreateRun(ModelCatalog catalog, AnalysisStore store,
        INodeRegistry registry, AnalysisSessions sessions, string id)
    {
        if (!store.Exists(id))
            return ApiResults.NotFound($"Analysis '{id}' not found");
        var snapshot = sessions.Snapshot(id);
        var inputs = RunInputs.Derive(snapshot.Document, registry, catalog);
        var record = RunRecorder.Freeze(snapshot, registry, inputs, EngineVersion, DateTimeOffset.UtcNow);
        try
        {
            var fileName = store.SaveRun(id, record);
            return ApiResults.Json(new RunSummary(fileName, record.TimestampUtc, record.GraphHash));
        }
        catch (IOException e)
        {
            return ApiResults.Conflict(e.Message);
        }
    }

    private static async Task<IResult> StreamEvents(HttpContext context,
        AnalysisStore store, AnalysisSessions sessions, string id)
    {
        if (!store.Exists(id))
            return ApiResults.NotFound($"Analysis '{id}' not found");

        var response = context.Response;
        response.Headers.ContentType = "text/event-stream";
        response.Headers.CacheControl = "no-cache";
        var ct = context.RequestAborted;
        var channel = Channel.CreateUnbounded<EvalUpdate>();
        using var subscription = sessions.Subscribe(id,
            snapshot => channel.Writer.TryWrite(snapshot.ToEvalUpdate(id)));
        try
        {
            await WriteUpdate(response, sessions.Snapshot(id).ToEvalUpdate(id), ct);
            Task<EvalUpdate>? pending = null;
            while (!ct.IsCancellationRequested)
            {
                pending ??= channel.Reader.ReadAsync(ct).AsTask();
                var completed = await Task.WhenAny(pending, Task.Delay(KeepAliveInterval, ct));
                if (completed == pending)
                {
                    var update = await pending;
                    pending = null;
                    await WriteUpdate(response, update, ct);
                }
                else
                {
                    await Write(response, ": keep-alive\n\n", ct);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Client disconnected; the subscription is disposed on the way out.
        }
        return Results.Empty;
    }

    private static Task WriteUpdate(HttpResponse response, EvalUpdate update, CancellationToken ct)
        => Write(response, "data: " + ApiJson.Serialize(update) + "\n\n", ct);

    private static async Task Write(HttpResponse response, string text, CancellationToken ct)
    {
        await response.WriteAsync(text, ct);
        await response.Body.FlushAsync(ct);
    }
}
