using System.Collections.Concurrent;
using Ara3D.NodeGraph;
using BimOpenFlow.Contracts;

namespace BimOpenFlow.Host.Api;

/// <summary>Keeps each analysis's last schema pass, keyed by the document's graph hash and the graph-input
/// values it was run with. The pass moves no data, but a SQL or file schema costs a catalog call, and the
/// events stream asks again on every evaluation; an edit changes the hash, so only an edited graph recomputes.</summary>
internal sealed class SchemaPassCache
{
    private readonly ConcurrentDictionary<string, (string Key, IReadOnlyList<PortSchema> Ports)> _last = new();

    public IReadOnlyList<PortSchema> Get(string analysisId, GraphDocument doc,
        IReadOnlyDictionary<string, string> inputs, Func<IReadOnlyList<PortSchema>> compute)
    {
        var key = KeyOf(doc, inputs);
        if (_last.TryGetValue(analysisId, out var kept) && kept.Key == key)
            return kept.Ports;
        var ports = compute();
        _last[analysisId] = (key, ports);
        return ports;
    }

    /// <summary>Forgets every pass, for when the files behind the catalog may have changed.</summary>
    public void Clear()
        => _last.Clear();

    private static string KeyOf(GraphDocument doc, IReadOnlyDictionary<string, string> inputs)
        => doc.ComputeGraphHash() + "|" + string.Join(";",
            inputs.OrderBy(kv => kv.Key, StringComparer.Ordinal).Select(kv => $"{kv.Key}={kv.Value}"));
}
