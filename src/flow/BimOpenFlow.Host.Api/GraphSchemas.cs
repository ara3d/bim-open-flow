using Ara3D.DataFlowEngine.Abstractions;
using Ara3D.NodeGraph;

namespace BimOpenFlow.Host.Api;

/// <summary>The static schema pass over a graph (TKT-209): the columns of every Table and Relation
/// output, found without evaluating any node that reads or computes data. A node declares its schema
/// as Static, Inferred (from its inputs, parameters and the catalog) or Dynamic; an output that cannot
/// be known says which node and why, so a picker that is empty can explain itself.</summary>
public static class GraphSchemas
{
    /// <summary>One <see cref="InferredPort"/> per Table or Relation output, in evaluation order. Graph input
    /// references take <paramref name="graphInputValues"/>, else the input's default. A document that does
    /// not validate against the registry has no schemas (the editor shows its errors instead).</summary>
    public static IReadOnlyList<InferredPort> Infer(GraphDocument document, INodeRegistry registry,
        ISchemaCatalog catalog, IReadOnlyDictionary<string, string>? graphInputValues = null)
    {
        if (document.Validate(registry).Count > 0)
            return [];
        var errors = new List<GraphError>();
        var resolved = document.Resolve(graphInputValues, errors);
        return errors.Count > 0
            ? []
            : new SchemaWalk(document.WithResolvedValues(resolved), registry, catalog).Run();
    }
}
