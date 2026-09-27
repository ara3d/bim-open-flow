using Ara3D.DataFlowEngine;
using Ara3D.DataFlowEngine.Abstractions;
using Ara3D.NodeGraph;

namespace BimOpenFlow.GraphText;

/// <summary>The structure half of graph text: node order and one binding line per node.</summary>
public static class Bindings
{
    /// <summary>Topological order with ties by node id (the engine's evaluation order); a
    /// document the engine cannot sort (a cycle, an edge to a missing node) falls back to id order.</summary>
    public static IReadOnlyList<GraphNode> Order(GraphDocument doc)
    {
        try
        {
            return doc.Sort();
        }
        catch (Exception e) when (e is InvalidOperationException or KeyNotFoundException)
        {
            return doc.Nodes.OrderBy(n => n.Id, StringComparer.Ordinal).ToList();
        }
    }

    /// <summary>Edges into the node by input port; a port wired twice (invalid) keeps its first edge.</summary>
    public static IReadOnlyDictionary<string, PortRef> Incoming(GraphDocument doc, string nodeId)
        => doc.Edges
            .Where(e => e.ToRef.NodeId == nodeId)
            .GroupBy(e => e.ToRef.Port)
            .ToDictionary(g => g.Key, g => g.First().FromRef);

    /// <summary><c>id = kind@version(port: node.port, ..., param: "value", ...);</c> with inputs in
    /// catalog order, then params in catalog order; names the catalog lacks follow in ordinal order.</summary>
    public static string Line(GraphDocument doc, GraphNode node, NodeSpec? spec)
    {
        var incoming = Incoming(doc, node.Id);
        var values = doc.Values.GetValueOrDefault(node.Id) ?? new Dictionary<string, string>();
        var inputs = CatalogOrder(incoming.Keys, spec?.Inputs.Select(p => p.Name))
            .Select(port => $"{port}: {incoming[port]}");
        var parameters = CatalogOrder(values.Keys, spec?.Params.Select(p => p.Name))
            .Select(name => $"{name}: {Literals.Quote(values[name])}");
        return $"{node.Id} = {node.Kind}@{node.Version}({string.Join(", ", inputs.Concat(parameters))});";
    }

    private static IReadOnlyList<string> CatalogOrder(IEnumerable<string> present, IEnumerable<string>? catalog)
    {
        var names = present.ToHashSet(StringComparer.Ordinal);
        var known = (catalog ?? []).Where(names.Contains).ToList();
        return [.. known, .. names.Except(known).Order(StringComparer.Ordinal)];
    }
}
