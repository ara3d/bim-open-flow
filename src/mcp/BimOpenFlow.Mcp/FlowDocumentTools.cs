using Ara3D.MCP;
using Ara3D.NodeGraph;
using BimOpenFlow.Host.Api;

namespace BimOpenFlow.Mcp;

/// <summary>Tools over the analysis library and model catalog: list, read, and
/// save whole documents, and describe the node vocabulary.</summary>
public static class FlowDocumentTools
{
    public static McpServer RegisterDocumentTools(this McpServer mcp, FlowServices s)
        => mcp
            .Tool(
                "listModels",
                "Lists the model files (.ifc, .bos) the host knows about, with their ids and paths.",
                (_, _) => ToolRunner.RunAsync(() => ListModels(s)))
            .Tool(
                "listAnalyses",
                "Lists the analyses in the library with their names and graph hashes.",
                (_, _) => ToolRunner.RunAsync(() => ListAnalyses(s), ["getAnalysis", "evaluate"]))
            .Tool(
                "getAnalysis",
                "Returns one analysis document as canonical graph JSON, plus its hash.",
                FlowToolArgs.Analysis().Build(),
                (args, _) => ToolRunner.RunAsync(
                    () => GetAnalysis(s, args.AnalysisId()),
                    ["evaluate", "getResult"]))
            .Tool(
                "saveAnalysis",
                "Validates a whole graph document against the node catalog and saves it as the new "
                + "current version (creating the analysis if needed). Prefer addNode/connect/setParam "
                + "for incremental edits.",
                FlowToolArgs.Analysis()
                    .String("json", "The graph document (.dfg.json content).", required: true)
                    .Build(),
                (args, _) => ToolRunner.RunAsync(
                    () => SaveAnalysis(s, args.AnalysisId(), args.GetRequiredString("json")),
                    ["evaluate"]))
            .Tool(
                "getNodeCatalog",
                "Describes every available node kind: ports, parameters, and capability.",
                (_, _) => ToolRunner.RunAsync(() => GetNodeCatalog(s), ["addNode"]));

    public static object ListModels(FlowServices s)
        => s.Host.Catalog.Scan()
            .Select(e => new
            {
                id = e.Id,
                name = e.Name,
                kind = e.Kind.ToString(),
                sizeBytes = e.SizeBytes,
                lastWriteUtc = e.LastWriteUtc,
                sourcePath = e.SourcePath,
            })
            .ToList();

    public static object ListAnalyses(FlowServices s)
        => s.Host.Store.List()
            .Select(a => new
            {
                id = a.Id,
                name = a.Name,
                graphHash = s.Host.Store.Load(a.Id).ComputeGraphHash(),
            })
            .ToList();

    public static object GetAnalysis(FlowServices s, string id)
    {
        var doc = Load(s, id);
        return new { id, graphHash = doc.ComputeGraphHash(), json = doc.ToCanonicalJson() };
    }

    public static object SaveAnalysis(FlowServices s, string id, string json)
        => SaveValidated(s, id, GraphDocumentIO.Parse(json));

    public static object GetNodeCatalog(FlowServices s)
        => s.Host.Registry.Nodes
            .Select(n => n.Spec)
            .OrderBy(spec => spec.Kind, StringComparer.Ordinal)
            .ThenBy(spec => spec.Version)
            .Select(spec => new
            {
                kind = spec.Kind,
                version = spec.Version,
                capability = spec.Capability.ToString(),
                inputs = spec.Inputs.Select(p => new { name = p.Name, type = p.Type.ToString() }).ToList(),
                outputs = spec.Outputs.Select(p => new { name = p.Name, type = p.Type.ToString() }).ToList(),
                @params = spec.Params.Select(p => new
                {
                    name = p.Name,
                    kind = p.Kind.ToString(),
                    @default = p.Default,
                    enumValues = p.EnumValues,
                }).ToList(),
                description = spec.Description,
            })
            .ToList();

    /// <summary>Validate first, then save and set the standing session — the same
    /// order as the HTTP PUT, so invalid documents never land in the store.</summary>
    internal static object SaveValidated(FlowServices s, string id, GraphDocument doc)
    {
        var errors = doc.Validate(s.Host.Registry);
        if (errors.Count > 0)
            throw new ArgumentException(
                "Invalid graph: " + string.Join("; ", errors.Select(e => e.Message)));
        s.Host.Store.Save(id, doc);
        s.Sessions.Set(id, doc);
        return new { id, graphHash = doc.ComputeGraphHash() };
    }

    internal static GraphDocument Load(FlowServices s, string id)
        => s.Host.Store.Exists(id)
            ? s.Host.Store.Load(id)
            : throw new FileNotFoundException($"Analysis '{id}' not found");
}
