using Ara3D.DataFlowEngine;
using Ara3D.DataFlowEngine.Abstractions;
using BimOpenFlow.Host.Store;
using BimOpenFlow.Contracts;
using EngineNodeStatus = Ara3D.DataFlowEngine.NodeStatus;
using SuggestKind = Ara3D.DataFlowEngine.Abstractions.SuggestKind;

namespace BimOpenFlow.Host.Api;

/// <summary>Lists the tables in a database file. Supplied by the host
/// composition, because Host.Api references no node pack.</summary>
public delegate IReadOnlyList<Suggestion> FileTableProbe(string path);

/// <summary>
/// Live parameter suggestions, resolved from the standing eval session per the
/// parameter's declared SuggestSource. Advisory: values remain free strings.
/// </summary>
public static class SuggestEndpoints
{
    public static void MapSuggestEndpoints(this IEndpointRouteBuilder app,
        AnalysisStore store, INodeRegistry registry, AnalysisSessions sessions,
        FileTableProbe? fileTables)
    {
        app.MapGet(ApiRoutes.GetSuggestions, (string id, string nodeId, string param)
            => ApiResults.Guard(() =>
                store.Exists(id)
                    ? GetSuggestions(sessions.Snapshot(id), registry, nodeId, param, fileTables)
                    : ApiResults.NotFound($"Analysis '{id}' not found")));
    }

    private static IResult GetSuggestions(EvalSnapshot snapshot, INodeRegistry registry,
        string nodeId, string param, FileTableProbe? fileTables)
    {
        var node = snapshot.Document.FindNode(nodeId);
        if (node is null)
            return ApiResults.NotFound($"Node '{nodeId}' not in analysis");
        var spec = registry.Find(node.Kind, node.Version)!.Spec;
        var paramSpec = spec.Params.FirstOrDefault(p => p.Name == param);
        if (paramSpec is null)
            return ApiResults.NotFound($"Node '{nodeId}' has no parameter '{param}'");
        if (paramSpec.Suggest is null)
            return ApiResults.NotFound($"Parameter '{param}' of '{node.Kind}' declares no suggestions");
        return ApiResults.Json(Resolve(snapshot, registry, nodeId, paramSpec.Suggest, fileTables));
    }

    /// <summary>Pure resolution of one SuggestSource against a snapshot.</summary>
    public static SuggestionList Resolve(EvalSnapshot snapshot, INodeRegistry registry,
        string nodeId, SuggestSource suggest, FileTableProbe? fileTables)
        => suggest.Kind switch
        {
            SuggestKind.ColumnsOfInput => ColumnsOfInput(snapshot, registry, nodeId, suggest.Source),
            SuggestKind.TablesInFile => TablesInFile(snapshot, nodeId, suggest.Source, fileTables),
            _ => Unavailable($"Unknown suggestion kind '{suggest.Kind}'"),
        };

    private static SuggestionList ColumnsOfInput(EvalSnapshot snapshot, INodeRegistry registry,
        string nodeId, string port)
    {
        var edge = snapshot.Document.Edges
            .FirstOrDefault(e => e.ToRef.NodeId == nodeId && e.ToRef.Port == port);
        if (edge is null)
            return Unready($"Connect a table to '{port}' to see columns");
        var upstream = snapshot.Results.GetValueOrDefault(edge.FromRef.NodeId);
        if (upstream is null || upstream.Status != EngineNodeStatus.Ok)
            return Unavailable(upstream?.Error
                ?? $"Upstream '{edge.FromRef.NodeId}' has no result (status {upstream?.Status.ToString() ?? "unknown"})");
        var upNode = snapshot.Document.FindNode(edge.FromRef.NodeId)!;
        var upSpec = registry.Find(upNode.Kind, upNode.Version)!.Spec;
        var index = upSpec.Outputs.ToList().FindIndex(o => o.Name == edge.FromRef.Port);
        if (index < 0 || upstream.Outputs[index] is not TableValue table)
            return Unavailable($"Output '{edge.From}' is not a table");
        return Ok(table.Table.Columns
            .Select(c => new Suggestion(c.Descriptor.Name,
                ValueHash.ToColumnKind(c.Descriptor.Type).ToString()))
            .ToList());
    }

    private static SuggestionList TablesInFile(EvalSnapshot snapshot,
        string nodeId, string pathParam, FileTableProbe? fileTables)
    {
        var path = snapshot.Document.Values.GetValueOrDefault(nodeId)?.GetValueOrDefault(pathParam) ?? "";
        if (path.Length == 0)
            return Unready($"Set '{pathParam}' to see tables");
        if (fileTables is null)
            return Unavailable("This host has no file-table probe");
        try
        {
            return Ok(fileTables(path));
        }
        catch (Exception e)
        {
            return Unavailable(e.Message);
        }
    }

    private static SuggestionList Ok(IReadOnlyList<Suggestion> values)
        => new(SuggestStatus.Ok, values, null);

    private static SuggestionList Unready(string reason)
        => new(SuggestStatus.Unready, Array.Empty<Suggestion>(), reason);

    private static SuggestionList Unavailable(string reason)
        => new(SuggestStatus.Unavailable, Array.Empty<Suggestion>(), reason);
}
