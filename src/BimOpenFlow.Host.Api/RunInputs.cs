using Ara3D.DataFlowEngine.Abstractions;
using Ara3D.DataFlowEngine.Runs;
using Ara3D.NodeGraph;
using BimOpenFlow.Host.Catalog;

namespace BimOpenFlow.Host.Api;

/// <summary>
/// Derives the pinned external inputs for a run: ModelRef params resolve
/// through the catalog to the model's content hash, FilePath params hash the
/// file directly. Unresolvable references are skipped (the run still freezes).
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
                if (!values.TryGetValue(param.Name, out var value) || value.Length == 0)
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
}
