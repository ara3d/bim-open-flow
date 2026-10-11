using Ara3D.DataFlowEngine;
using Ara3D.DataFlowEngine.Abstractions;
using Ara3D.DataFlowEngine.Runs;
using Ara3D.NodeGraph;
using BimOpenFlow.Host.Catalog;

namespace BimOpenFlow.Host.Api;

/// <summary>
/// Derives the pinned external inputs for a run: ModelRef params resolve
/// through the catalog to the model's content hash, FilePath params hash the
/// file directly. Unresolvable references are skipped (the run still freezes).
/// A parameter that an edge feeds is bound: its stored string is ignored by the
/// engine, so the document overload skips it and the snapshot overload pins the
/// wired value instead. A parameter that refers to a graph input is read at its resolved value,
/// so pass the snapshot's ResolvedDocument() to the document overload.
/// </summary>
public static class RunInputs
{
    // TODO: catalog.Scan() re-hashes every model file per run; memoize when model trees grow.
    public static IReadOnlyList<RunInput> Derive(GraphDocument doc, INodeRegistry registry, ModelCatalog catalog)
    {
        var inputs = new List<RunInput>();
        IReadOnlyList<ModelEntry>? models = null;
        foreach (var node in doc.Nodes)
        {
            var spec = registry.Find(node.Kind, node.Version)?.Spec;
            var values = doc.Values.GetValueOrDefault(node.Id);
            if (spec is null || values is null)
                continue;
            foreach (var param in spec.Params)
            {
                if (!values.TryGetValue(param.Name, out var value) || value.Length == 0
                    || IsWired(doc, node.Id, param.Name))
                    continue;
                var hash = param.Kind switch
                {
                    ParamKind.ModelRef => (models ??= catalog.Scan())
                        .FirstOrDefault(m => m.Id == value)?.ContentHash,
                    ParamKind.FilePath => File.Exists(value) ? ModelCatalog.HashFile(value) : null, // TODO: glob paths (data/*.csv) are skipped, so runs omit provenance for glob-read files
                    _ => null,
                };
                if (hash is not null)
                    inputs.Add(new(node.Id, param.Name, hash, value));
            }
        }
        return inputs;
    }

    private static bool IsWired(GraphDocument doc, string nodeId, string param)
        => doc.Edges.Any(e => e.ToRef.NodeId == nodeId && e.ToRef.Port == param);

    /// <summary>As the document overload, plus: every Database output of an Ok node, pinned by its
    /// content hash as RunInput(node, "source", hash, path) unless the node's own FilePath parameter
    /// already pinned that hash; every wired FilePath parameter pinned at its consumer by the hash of
    /// the Database value on the wire, as RunInput(consumer, parameter, hash, path); and, when relations is given, each file relations.SourceFiles returns
    /// for an Ok node's RelationValue output, recorded the same way with the file's sha256.</summary>
    public static IReadOnlyList<RunInput> Derive(EvalSnapshot snapshot, INodeRegistry registry,
        ModelCatalog catalog, IRelationResults? relations)
    {
        var inputs = Derive(snapshot.ResolvedDocument(), registry, catalog).ToList();
        foreach (var (nodeId, result) in snapshot.Results)
        {
            if (result.Status != NodeStatus.Ok)
                continue;
            foreach (var output in result.Outputs)
            {
                if (output is DatabaseValue database)
                {
                    if (!inputs.Any(i => i.Node == nodeId && i.ContentHash == database.Hash))
                        inputs.Add(new(nodeId, "source", database.Hash, database.Path));
                }
                else if (output is RelationValue relation && relations is not null)
                {
                    foreach (var file in relations.SourceFiles(relation))
                        inputs.Add(new(nodeId, "source", ModelCatalog.HashFile(file), file));
                }
            }
        }
        PinWiredFilePaths(snapshot, registry, inputs);
        return inputs;
    }

    private static void PinWiredFilePaths(EvalSnapshot snapshot, INodeRegistry registry, List<RunInput> inputs)
    {
        var doc = snapshot.Document;
        foreach (var edge in doc.Edges)
        {
            var target = doc.Nodes.FirstOrDefault(n => n.Id == edge.ToRef.NodeId);
            var param = target is null ? null
                : registry.Find(target.Kind, target.Version)?.Spec.Params.FirstOrDefault(p => p.Name == edge.ToRef.Port);
            if (param?.Kind != ParamKind.FilePath)
                continue;
            var origin = doc.Nodes.FirstOrDefault(n => n.Id == edge.FromRef.NodeId);
            var outputs = origin is null ? null : registry.Find(origin.Kind, origin.Version)?.Spec.Outputs;
            var index = outputs?.ToList().FindIndex(o => o.Name == edge.FromRef.Port) ?? -1;
            if (index < 0 || !snapshot.Results.TryGetValue(edge.FromRef.NodeId, out var result)
                || result.Status != NodeStatus.Ok || index >= result.Outputs.Count)
                continue;
            if (result.Outputs[index] is DatabaseValue database)
                inputs.Add(new(target!.Id, param.Name, database.Hash, database.Path));
        }
    }
}
