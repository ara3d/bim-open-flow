using System.Text.Json.Nodes;
using Ara3D.MCP;
using Ara3D.NodeGraph;

namespace BimOpenMcp.Flow;

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
                    .String("to", "Target input port, or bindable parameter (the catalog's 'bindable' field), e.g. 'filter.table' or 'doors.path'.", required: true)
                    .Build(),
                (args, _) => ToolRunner.RunAsync(
                    () => Connect(s, args.AnalysisId(), args.GetRequiredString("from"),
                        args.GetRequiredString("to")),
                    ["evaluate"]))
            .Tool(
                "setParam",
                "Sets one node parameter (values travel as canonical invariant strings). To change a graph "
                + "input instead, omit nodeId and give its name with a dollar sign ('$model'): the value becomes "
                + "the input's current value and every node that refers to it is evaluated again. The graph "
                + "document and its hash do not change.",
                FlowToolArgs.Analysis()
                    .String("nodeId", "The node to change. Omit for a graph input.")
                    .String("name", "Parameter name from the node's spec, or '$input' for a graph input.", required: true)
                    .String("value", "Parameter value as a string.", required: true)
                    .Build(),
                (args, _) => ToolRunner.RunAsync(
                    () => string.IsNullOrEmpty(args.GetString("nodeId"))
                        ? SetGraphInput(s, args.AnalysisId(), args.GetRequiredString("name"), args.GetRequiredString("value"))
                        : SetParam(s, args.AnalysisId(), args.GetRequiredString("nodeId"),
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
                        + "(ports as 'nodeId.port'; a bindable parameter takes an edge on its own name), removeNode {nodeId}, "
                        + "declareInput {name, kind (FilePath, Text, Number, Integer or Boolean), value (the default), description?} "
                        + "or removeInput {name}. A parameter value '$name' refers to a declared graph input. Example: "
                        + "[{\"op\":\"addNode\",\"nodeId\":\"database\",\"kind\":\"duck.source\"},"
                        + "{\"op\":\"setParam\",\"nodeId\":\"database\",\"name\":\"path\",\"value\":\"C:/data/model.duckdb\"},"
                        + "{\"op\":\"addNode\",\"nodeId\":\"doors\",\"kind\":\"duck.query\"},"
                        + "{\"op\":\"setParam\",\"nodeId\":\"doors\",\"name\":\"sql\",\"value\":\"SELECT element_mark AS Mark FROM door\"},"
                        + "{\"op\":\"connect\",\"from\":\"database.source\",\"to\":\"doors.path\"}]",
                        required: true)
                    .Build(),
                (args, _) => ToolRunner.RunAsync(
                    () => EditGraph(s, args.AnalysisId(), args.GetRequiredString("edits")),
                    ["evaluate"]));

    public sealed record GraphEdit(string Op, string? NodeId = null, string? Kind = null, int? Version = null,
        string? Name = null, string? Value = null, string? From = null, string? To = null, string? Description = null);

    public static object EditGraph(FlowServices s, string id, string editsJson)
    {
        JsonNode? parsed;
        try
        {
            parsed = JsonNode.Parse(EscapeControlCharactersInStrings(editsJson));
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
                throw new ArgumentException(
                    $"Nothing was saved; the graph is unchanged. Edit {i + 1} ({list[i].Op} {Describe(list[i])}): {e.Message}", e);
            }
        }
        try
        {
            FlowDocumentTools.SaveValidated(s, id, doc);
        }
        catch (ArgumentException e)
        {
            throw new ArgumentException($"Nothing was saved; the graph is unchanged. {e.Message}", e);
        }
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
            "declareInput" => doc.SetInput(new GraphInput(Require(edit.Name, "name"),
                Enum.Parse<Ara3D.DataFlowEngine.Abstractions.ParamKind>(Require(edit.Kind, "kind")),
                edit.Value ?? "", edit.Description ?? "")),
            "removeInput" => doc.RemoveInput(Require(edit.Name, "name")),
            _ => throw new ArgumentException(
                $"Unknown op '{edit.Op}'; expected addNode, setParam, connect, removeNode, declareInput, or removeInput"),
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
                Text(o, "name"), Text(o, "value"), Text(o, "from"), Text(o, "to"), Text(o, "description")))
            .ToList();
    }

    /// <summary>Models write SQL with real line breaks inside JSON strings, which
    /// strict JSON rejects. Escape control characters that sit inside string
    /// literals; whitespace between tokens is left alone.</summary>
    public static string EscapeControlCharactersInStrings(string json)
    {
        var sb = new System.Text.StringBuilder(json.Length + 16);
        var inString = false;
        var escaped = false;
        foreach (var ch in json)
        {
            if (inString)
            {
                if (escaped)
                {
                    sb.Append(ch);
                    escaped = false;
                    continue;
                }
                switch (ch)
                {
                    case '\\': sb.Append(ch); escaped = true; continue;
                    case '"': sb.Append(ch); inString = false; continue;
                    case '\n': sb.Append("\\n"); continue;
                    case '\r': sb.Append("\\r"); continue;
                    case '\t': sb.Append("\\t"); continue;
                    default:
                        if (ch < ' ')
                            sb.Append("\\u").Append(((int)ch).ToString("x4"));
                        else
                            sb.Append(ch);
                        continue;
                }
            }
            if (ch == '"')
                inString = true;
            sb.Append(ch);
        }
        return sb.ToString();
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
            "declareInput" or "removeInput" => e.Name ?? "",
            _ => e.NodeId ?? "",
        };

    public static object AddNode(FlowServices s, string id, string nodeId, string kind, int? version)
        => Edit(s, id, doc => doc.AddNode(nodeId, kind, version ?? LatestVersion(s, kind)));

    public static object Connect(FlowServices s, string id, string from, string to)
        => Edit(s, id, doc => doc.Connect(from, to));

    public static object SetParam(FlowServices s, string id, string nodeId, string name, string value)
        => Edit(s, id, doc => doc.SetParam(nodeId, name, value));

    /// <summary>Sets the current value of graph input <paramref name="name"/> (written '$model') and
    /// evaluates again. The document is untouched; the value is kept beside it in the store.</summary>
    public static object SetGraphInput(FlowServices s, string id, string name, string value)
    {
        if (!name.StartsWith(GraphInputs.Sigil))
            throw new ArgumentException(
                $"Give 'nodeId' to set a node parameter, or write the graph input as '${name}' to set it");
        if (!s.Host.Store.Exists(id))
            throw new FileNotFoundException($"Analysis '{id}' not found");
        var input = name[1..];
        var snapshot = s.Sessions.SetGraphInputs(id, new Dictionary<string, string> { [input] = value });
        return new
        {
            id,
            graphInput = input,
            value = snapshot.GraphInputs![input],
            graphHash = snapshot.Document.ComputeGraphHash(),
        };
    }

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
