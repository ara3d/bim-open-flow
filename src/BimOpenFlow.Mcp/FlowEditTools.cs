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
                    ["evaluate"]));

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
