using System.Text.Json.Nodes;
using Ara3D.MCP;
using Ara3D.NodeGraph;

namespace BimOpenFlow.Mcp;

/// <summary>Incremental graph editing: each tool loads the current document
/// (or starts empty), applies one GraphEditing operation, validates against the
/// registry, and saves — so agents never hand-edit JSON.</summary>
public static class FlowEditTools
{
    public static McpServer RegisterEditTools(this McpServer mcp, FlowServices s)
        => mcp
            .Tool(
                "addNode",
                "Adds a node to an analysis (creating the analysis if needed). Version defaults to "
                + "the latest in the node catalog.",
                FlowToolArgs.Analysis()
                    .String("nodeId", "New node id, unique in the graph, no dots.", required: true)
                    .String("kind", "Node kind from getNodeCatalog, e.g. 'bos.load'.", required: true)
                    .Integer("version", "Node version. Defaults to the latest for the kind.")
                    .Build(),
                (args, _) => ToolRunner.RunAsync(
                    () => AddNode(s, args.AnalysisId(), args.GetRequiredString("nodeId"),
                        args.GetRequiredString("kind"), args.GetInt("version")),
                    ["setParam", "connect", "evaluate"]))
            .Tool(
                "connect",
                "Adds an edge between two ports ('nodeId.port'). An input port takes one edge, so "
                + "any existing edge into the target is replaced.",
                FlowToolArgs.Analysis()
                    .String("from", "Source output port, e.g. 'load.entities'.", required: true)
                    .String("to", "Target input port, e.g. 'filter.table'.", required: true)
                    .Build(),
                (args, _) => ToolRunner.RunAsync(
                    () => Connect(s, args.AnalysisId(), args.GetRequiredString("from"),
                        args.GetRequiredString("to")),
                    ["evaluate"]))
            .Tool(
                "setParam",
                "Sets one node parameter (values travel as canonical invariant strings).",
                FlowToolArgs.Analysis()
                    .String("nodeId", "The node to change.", required: true)
                    .String("name", "Parameter name from the node's spec.", required: true)
                    .String("value", "Parameter value as a string.", required: true)
                    .Build(),
                (args, _) => ToolRunner.RunAsync(
                    () => SetParam(s, args.AnalysisId(), args.GetRequiredString("nodeId"),
                        args.GetRequiredString("name"), args.GetRequiredString("value")),
                    ["evaluate"]))
            .Tool(
                "removeNode",
                "Removes a node and everything that hangs off it: its edges, values, and layout.",
                FlowToolArgs.Analysis()
                    .String("nodeId", "The node to remove.", required: true)
                    .Build(),
                (args, _) => ToolRunner.RunAsync(
                    () => RemoveNode(s, args.AnalysisId(), args.GetRequiredString("nodeId")),
                    ["evaluate"]))
            .Tool(
                "editGraph",
                "Applies a list of edits to an analysis in one call and saves once (creating the "
                + "analysis if needed): the way to build or rewire a whole graph. All edits are "
                + "validated together; if any fails, nothing is saved and the error names the edit.",
                FlowToolArgs.Analysis()
                    .String("edits",
                        "A JSON array, as a string, of edit objects applied in order. Each has 'op': "
                        + "addNode {nodeId, kind, version?}, setParam {nodeId, name, value}, connect {from, to} "
                        + "(ports as 'nodeId.port'), or removeNode {nodeId}. Example: "
                        + "[{\"op\":\"addNode\",\"nodeId\":\"database\",\"kind\":\"duck.source\"},"
                        + "{\"op\":\"setParam\",\"nodeId\":\"database\",\"name\":\"path\",\"value\":\"C:/data/model.duckdb\"},"
                        + "{\"op\":\"addNode\",\"nodeId\":\"doors\",\"kind\":\"duck.query\"},"
                        + "{\"op\":\"setParam\",\"nodeId\":\"doors\",\"name\":\"sql\",\"value\":\"SELECT element_mark AS Mark FROM door\"},"
                        + "{\"op\":\"connect\",\"from\":\"database.source\",\"to\":\"doors.source\"}]",
                        required: true)
                    .Build(),
                (args, _) => ToolRunner.RunAsync(
                    () => EditGraph(s, args.AnalysisId(), args.GetRequiredString("edits")),
                    ["evaluate"]));

    public sealed record GraphEdit(string Op, string? NodeId = null, string? Kind = null, int? Version = null,
        string? Name = null, string? Value = null, string? From = null, string? To = null);

    public static object EditGraph(FlowServices s, string id, string editsJson)
    {
        JsonNode? parsed;
        try
        {
            parsed = JsonNode.Parse(editsJson);
        }
        catch (System.Text.Json.JsonException e)
        {
            throw new ArgumentException($"'edits' is not valid JSON: {e.Message}", e);
        }
        return EditGraph(s, id, parsed);
    }

    public static object EditGraph(FlowServices s, string id, JsonNode? edits)
    {
        var list = ParseEdits(edits);
        var doc = LoadOrEmpty(s, id);
        for (var i = 0; i < list.Count; i++)
        {
            try
            {
                doc = Apply(s, doc, list[i]);
            }
            catch (Exception e) when (e is ArgumentException or InvalidOperationException or KeyNotFoundException)
            {
                throw new ArgumentException($"Edit {i + 1} ({list[i].Op} {Describe(list[i])}): {e.Message}", e);
            }
        }
        FlowDocumentTools.SaveValidated(s, id, doc);
        return new { id, applied = list.Count, graphHash = doc.ComputeGraphHash() };
    }

    public static GraphDocument Apply(FlowServices s, GraphDocument doc, GraphEdit edit)
        => edit.Op switch
        {
            "addNode" => doc.AddNode(Require(edit.NodeId, "nodeId"), Require(edit.Kind, "kind"),
                edit.Version ?? LatestVersion(s, edit.Kind!)),
            "setParam" => doc.SetParam(Require(edit.NodeId, "nodeId"), Require(edit.Name, "name"), edit.Value ?? ""),
            "connect" => doc.Connect(Require(edit.From, "from"), Require(edit.To, "to")),
            "removeNode" => doc.RemoveNode(Require(edit.NodeId, "nodeId")),
            _ => throw new ArgumentException($"Unknown op '{edit.Op}'; expected addNode, setParam, connect, or removeNode"),
        };

    private static IReadOnlyList<GraphEdit> ParseEdits(JsonNode? edits)
    {
        if (edits is not JsonArray array || array.Count == 0)
            throw new ArgumentException("'edits' must be a non-empty array of edit objects");
        return array.Select((node, i) => node as JsonObject
                ?? throw new ArgumentException($"Edit {i + 1} is not an object"))
            .Select(o => new GraphEdit(
                o["op"]?.GetValue<string>() ?? throw new ArgumentException("An edit is missing 'op'"),
                Text(o, "nodeId"), Text(o, "kind"), o["version"]?.GetValue<int>(),
                Text(o, "name"), Text(o, "value"), Text(o, "from"), Text(o, "to")))
            .ToList();
    }

    private static string? Text(JsonObject o, string key)
        => o[key] is JsonValue v ? v.ToString() : null;

    private static string Require(string? value, string field)
        => string.IsNullOrWhiteSpace(value) ? throw new ArgumentException($"'{field}' is required") : value;

    private static string Describe(GraphEdit e)
        => e.Op switch
        {
            "connect" => $"{e.From} -> {e.To}",
            "setParam" => $"{e.NodeId}.{e.Name}",
            _ => e.NodeId ?? "",
        };

    public static object AddNode(FlowServices s, string id, string nodeId, string kind, int? version)
        => Edit(s, id, doc => doc.AddNode(nodeId, kind, version ?? LatestVersion(s, kind)));

    public static object Connect(FlowServices s, string id, string from, string to)
        => Edit(s, id, doc => doc.Connect(from, to));

    public static object SetParam(FlowServices s, string id, string nodeId, string name, string value)
        => Edit(s, id, doc => doc.SetParam(nodeId, name, value));

    public static object RemoveNode(FlowServices s, string id, string nodeId)
        => Edit(s, id, doc => doc.RemoveNode(nodeId));

    private static object Edit(FlowServices s, string id, Func<GraphDocument, GraphDocument> edit)
        => FlowDocumentTools.SaveValidated(s, id, edit(LoadOrEmpty(s, id)));

    private static GraphDocument LoadOrEmpty(FlowServices s, string id)
        => s.Host.Store.Exists(id) ? s.Host.Store.Load(id) : GraphDocument.Empty;

    private static int LatestVersion(FlowServices s, string kind)
        => s.Host.Registry.Nodes
            .Where(n => n.Spec.Kind == kind)
            .Select(n => n.Spec.Version)
            .DefaultIfEmpty()
            .Max() is var latest && latest > 0
            ? latest
            : throw new ArgumentException($"Unknown node kind '{kind}'; see getNodeCatalog");
}
