using System.Text.Json;
using BimOpenFlow.Contracts;
using BimOpenFlow.Host.Api;

namespace BimOpenFlow.Host;

/// <summary>The committed node catalog (docs/nodes.catalog.json): what GET /api/catalog/nodes
/// answers, over the nodes of every profile a host offers, so web tests can size node cards
/// without a running host.</summary>
public static class NodeCatalogFile
{
    public const string FileName = "nodes.catalog.json";

    private static readonly JsonSerializerOptions Indented = new(ApiJson.Options) { WriteIndented = true };

    /// <summary>The union of the profiles' catalogs, sorted by kind then version, as indented
    /// JSON with LF line endings and a final newline. Where two profiles carry the same kind
    /// and version, the first profile's spec is kept.</summary>
    public static string Text(HostProfiles profiles)
    {
        var nodes = profiles.All
            .SelectMany(profile => profile.Registry().ToCatalog().Nodes)
            .DistinctBy(node => (node.Kind, node.Version))
            .OrderBy(node => node.Kind, StringComparer.Ordinal)
            .ThenBy(node => node.Version)
            .ToList();
        return JsonSerializer.Serialize(new NodeCatalog(nodes), Indented).ReplaceLineEndings("\n") + "\n";
    }
}
