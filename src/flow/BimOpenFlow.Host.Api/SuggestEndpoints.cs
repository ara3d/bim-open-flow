using Ara3D.DataFlowEngine;
using Ara3D.DataFlowEngine.Abstractions;
using Ara3D.NodeGraph;
using BimOpenFlow.Host.Catalog;
using BimOpenFlow.Host.Store;
using BimOpenFlow.Contracts;
using EngineNodeStatus = Ara3D.DataFlowEngine.NodeStatus;
using SuggestKind = Ara3D.DataFlowEngine.Abstractions.SuggestKind;
using ColumnType = Ara3D.DataFlowEngine.Abstractions.ColumnType;

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
        FileTableProbe? fileTables, IRelationResults? relations = null, ModelRootFiles? rootFiles = null)
    {
        app.MapGet(ApiRoutes.GetSuggestions, (string id, string nodeId, string param)
            => ApiResults.Guard(() =>
                store.Exists(id)
                    ? GetSuggestions(id, sessions, registry, nodeId, param, fileTables, relations, rootFiles)
                    : ApiResults.NotFound($"Analysis '{id}' not found")));
    }

    private static IResult GetSuggestions(string id, AnalysisSessions sessions, INodeRegistry registry,
        string nodeId, string param, FileTableProbe? fileTables, IRelationResults? relations,
        ModelRootFiles? rootFiles)
    {
        var snapshot = sessions.Snapshot(id);
        var node = snapshot.Document.FindNode(nodeId);
        if (node is null)
            return ApiResults.NotFound($"Node '{nodeId}' not in analysis");
        var spec = registry.Find(node.Kind, node.Version)!.Spec;
        var paramSpec = spec.Params.FirstOrDefault(p => p.Name == param);
        if (paramSpec is null)
            return ApiResults.NotFound($"Node '{nodeId}' has no parameter '{param}'");
        if (paramSpec.Suggest is null)
            return ApiResults.NotFound($"Parameter '{param}' of '{node.Kind}' declares no suggestions");
        return ApiResults.Json(Resolve(snapshot, registry, nodeId, paramSpec.Suggest, fileTables, relations, rootFiles,
            paramSpec.Suggest.Kind == SuggestKind.ColumnsOfInput ? sessions.Schemas(id, snapshot) : null));
    }

    /// <summary>Pure resolution of one SuggestSource against a snapshot. <paramref name="schemas"/> is the
    /// static schema pass over the snapshot's document; column suggestions read it before any result.</summary>
    public static SuggestionList Resolve(EvalSnapshot snapshot, INodeRegistry registry,
        string nodeId, SuggestSource suggest, FileTableProbe? fileTables, IRelationResults? relations = null, ModelRootFiles? rootFiles = null,
        IReadOnlyList<PortSchema>? schemas = null)
        => suggest.Kind switch
        {
            SuggestKind.ColumnsOfInput => ColumnsOfInput(snapshot, registry, nodeId, suggest, relations, schemas ?? []),
            SuggestKind.TablesInFile => TablesInFile(snapshot, nodeId, suggest.Source, fileTables),
            SuggestKind.FilesUnderRoots => FilesUnderRoots(suggest.Source, rootFiles),
            _ => Unavailable($"Unknown suggestion kind '{suggest.Kind}'"),
        };

    private static SuggestionList ColumnsOfInput(EvalSnapshot snapshot, INodeRegistry registry,
        string nodeId, SuggestSource suggest, IRelationResults? relations, IReadOnlyList<PortSchema> schemas)
    {
        var columns = ColumnsOfPort(snapshot, registry, nodeId, suggest.Source, relations, schemas);
        if (columns.Failure is not null)
            return columns.Failure;
        var other = suggest.SharedWith is { } shared
            ? ColumnsOfPort(snapshot, registry, nodeId, shared, relations, schemas).Columns
            : null;
        return Ok(ColumnSuggestions.Build(columns.Columns!, suggest.Use, other));
    }

    /// <summary>The columns arriving at an input port: the inferred schema of the upstream output first,
    /// because it exists before any run, then the recorded columns of its result.</summary>
    private static (IReadOnlyList<SuggestedColumn>? Columns, SuggestionList? Failure) ColumnsOfPort(
        EvalSnapshot snapshot, INodeRegistry registry, string nodeId, string port,
        IRelationResults? relations, IReadOnlyList<PortSchema> schemas)
    {
        var edge = snapshot.Document.Edges
            .FirstOrDefault(e => e.ToRef.NodeId == nodeId && e.ToRef.Port == port);
        if (edge is null)
            return (null, Unready($"Connect a table to '{port}' to see columns"));
        var inferred = schemas.FirstOrDefault(p => p.Node == edge.FromRef.NodeId && p.Output == edge.FromRef.Port);
        if (inferred is { Status: SchemaStatus.Ok, Columns: { } known })
            return (known.Select(c => new SuggestedColumn(c.Name, Enum.Parse<ColumnType>(c.Type.ToString()), c.Kind)).ToList(), null);
        var recorded = RecordedColumns(snapshot, registry, edge, relations);
        return recorded.Columns is null && inferred?.Reason is { } reason && recorded.Failure!.Status == SuggestStatus.Unavailable
            ? (null, Unavailable(reason))
            : recorded;
    }

    private static (IReadOnlyList<SuggestedColumn>? Columns, SuggestionList? Failure) RecordedColumns(
        EvalSnapshot snapshot, INodeRegistry registry, GraphEdge edge, IRelationResults? relations)
    {
        var upstream = snapshot.Results.GetValueOrDefault(edge.FromRef.NodeId);
        if (upstream is null || upstream.Status != EngineNodeStatus.Ok)
            return (null, Unavailable(upstream?.Error
                ?? $"Upstream '{edge.FromRef.NodeId}' has no result (status {upstream?.Status.ToString() ?? "unknown"})"));
        var upNode = snapshot.Document.FindNode(edge.FromRef.NodeId)!;
        var upSpec = registry.Find(upNode.Kind, upNode.Version)!.Spec;
        var index = upSpec.Outputs.ToList().FindIndex(o => o.Name == edge.FromRef.Port);
        if (index >= 0 && upstream.Outputs[index] is RelationValue relation)
            return relations is null
                ? (null, Unavailable("This host has no relation executor"))
                : (relations.Columns(relation).Select(c => new SuggestedColumn(c.Value, ParseType(c.Detail), null)).ToList(), null);
        if (index < 0 || upstream.Outputs[index] is not TableValue table)
            return (null, Unavailable($"Output '{edge.From}' is not a table"));
        return (table.Table.Columns
            .Select(c => new SuggestedColumn(c.Descriptor.Name, ParseType(ValueHash.ToColumnKind(c.Descriptor.Type).ToString()), null))
            .ToList(), null);
    }

    private static ColumnType ParseType(string? name)
        => Enum.TryParse<ColumnType>(name, out var type)
            ? type : ColumnType.Unknown;

    private static SuggestionList TablesInFile(EvalSnapshot snapshot,
        string nodeId, string pathParam, FileTableProbe? fileTables)
    {
        var path = snapshot.ResolvedDocument().Values.GetValueOrDefault(nodeId)?.GetValueOrDefault(pathParam) ?? "";
        var wired = snapshot.Document.Edges.FirstOrDefault(e => e.ToRef.NodeId == nodeId && e.ToRef.Port == pathParam);
        if (wired is not null)
        {
            var upstream = snapshot.Results.GetValueOrDefault(wired.FromRef.NodeId);
            var database = upstream?.Status == EngineNodeStatus.Ok
                ? upstream.Outputs.OfType<DatabaseValue>().FirstOrDefault()
                : null;
            if (database is null)
                return Unavailable(upstream?.Error ?? $"Upstream '{wired.FromRef.NodeId}' has no database yet");
            path = database.Path;
        }
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

    private static SuggestionList FilesUnderRoots(string extensions, ModelRootFiles? rootFiles)
    {
        if (rootFiles is null)
            return Unavailable("This host has no model roots");
        var files = rootFiles.List(extensions, out var truncated);
        var list = new SuggestionList(SuggestStatus.Ok,
            files.Select(f => new Suggestion(f.Path, f.Relative)).ToList(),
            truncated ? $"Only the first {ModelRootFiles.MaxFiles} files are listed; browse for the rest" : null);
        return list;
    }

    private static SuggestionList Ok(IReadOnlyList<Suggestion> values)
        => new(SuggestStatus.Ok, values, null);

    private static SuggestionList Unready(string reason)
        => new(SuggestStatus.Unready, Array.Empty<Suggestion>(), reason);

    private static SuggestionList Unavailable(string reason)
        => new(SuggestStatus.Unavailable, Array.Empty<Suggestion>(), reason);
}
