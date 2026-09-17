using Ara3D.DataFlowEngine;
using Ara3D.DataFlowEngine.Runs;
using Ara3D.MCP;
using BimOpenFlow.Host.Api;
using BimOpenFlow.Host.Store;

namespace BimOpenFlow.Mcp;

/// <summary>Evaluation state, result paging, and run archival — the same
/// store/session semantics as the HTTP API.</summary>
public static class FlowEvalTools
{
    public static readonly string EngineVersion =
        typeof(EvalSession).Assembly.GetName().Version?.ToString(3) ?? "0.0.0";

    public static McpServer RegisterEvalTools(this McpServer mcp, FlowServices s)
        => mcp
            .Tool(
                "evaluate",
                "Evaluates the current document (through the standing session) and returns a state "
                + "summary per node: Ok, Unready, EffectPending, Unavailable, or Error.",
                FlowToolArgs.Analysis().Build(),
                (args, _) => ToolRunner.RunAsync(
                    () => Evaluate(s, args.AnalysisId()),
                    ["getResult", "createRun"]))
            .Tool(
                "getResult",
                "Returns one node output as a paged table slice; a scalar output becomes a "
                + "one-cell slice.",
                FlowToolArgs.Analysis()
                    .String("nodeId", "The node whose output to read.", required: true)
                    .String("port", "The output port name.", required: true)
                    .Paged()
                    .Build(),
                (args, _) => ToolRunner.RunAsync(
                    () => GetResult(s, args.AnalysisId(), args.GetRequiredString("nodeId"),
                        args.GetRequiredString("port"), args.Skip(), args.Take())))
            .Tool(
                "listRuns",
                "Lists the archived runs of an analysis, oldest first.",
                FlowToolArgs.Analysis().Build(),
                (args, _) => ToolRunner.RunAsync(() => ListRuns(s, args.AnalysisId())))
            .Tool(
                "createRun",
                "Freezes the current evaluation as an immutable run record (pinning model/file "
                + "inputs by content hash) and archives it.",
                FlowToolArgs.Analysis().Build(),
                (args, _) => ToolRunner.RunAsync(
                    () => CreateRun(s, args.AnalysisId()),
                    ["listRuns"]));

    public static object Evaluate(FlowServices s, string id)
    {
        Require(s, id);
        var snapshot = s.Sessions.Snapshot(id);
        return new
        {
            analysisId = id,
            nodes = snapshot.Results.Values
                .OrderBy(r => r.NodeId, StringComparer.Ordinal)
                .Select(r => new
                {
                    nodeId = r.NodeId,
                    status = r.Status.ToString(),
                    error = r.Error,
                    blockingNodeId = r.BlockingNodeId,
                    warnings = r.Warnings,
                })
                .ToList(),
            warnings = snapshot.Warnings,
        };
    }

    public static object GetResult(FlowServices s, string id, string nodeId, string port, int skip, int take)
    {
        Require(s, id);
        var snapshot = s.Sessions.Snapshot(id);
        var node = snapshot.Document.FindNode(nodeId)
            ?? throw new ArgumentException($"Node '{nodeId}' not in analysis '{id}'");
        var spec = s.Host.Registry.Find(node.Kind, node.Version)!.Spec;
        var index = IndexOfOutput(spec.Outputs.Select(p => p.Name).ToList(), port)
            is var i and >= 0 ? i : throw new ArgumentException($"Node '{nodeId}' has no output port '{port}'");
        var result = snapshot.Results.GetValueOrDefault(nodeId);
        if (result is null || result.Status != NodeStatus.Ok)
            throw new InvalidOperationException(
                $"No result for '{nodeId}.{port}' (status {result?.Status.ToString() ?? "unknown"})");
        var slice = result.Outputs[index].ToSlice(port, skip, take);
        return new
        {
            columns = slice.Columns.Select(c => new { name = c.Name, type = c.Type.ToString() }).ToList(),
            rows = slice.Rows,
            totalRows = slice.TotalRows,
            skip = slice.Skip,
        };
    }

    public static object ListRuns(FlowServices s, string id)
    {
        Require(s, id);
        return s.Host.Store.ListRuns(id)
            .Select(fileName => ToSummary(fileName, s.Host.Store.LoadRun(id, fileName)))
            .ToList();
    }

    public static object CreateRun(FlowServices s, string id)
    {
        Require(s, id);
        var snapshot = s.Sessions.Snapshot(id);
        var inputs = RunInputs.Derive(snapshot.Document, s.Host.Registry, s.Host.Catalog);
        var record = RunRecorder.Freeze(snapshot, s.Host.Registry, inputs, EngineVersion, DateTimeOffset.UtcNow);
        return ToSummary(s.Host.Store.SaveRun(id, record), record);
    }

    private static object ToSummary(string fileName, RunRecord record)
        => new { fileName, timestampUtc = record.TimestampUtc, graphHash = record.GraphHash };

    private static void Require(FlowServices s, string id)
    {
        if (!s.Host.Store.Exists(id))
            throw new FileNotFoundException($"Analysis '{id}' not found");
    }

    private static int IndexOfOutput(IReadOnlyList<string> names, string port)
    {
        for (var i = 0; i < names.Count; i++)
            if (names[i] == port)
                return i;
        return -1;
    }
}
