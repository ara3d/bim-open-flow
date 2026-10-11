using Ara3D.NodeGraph;
using BimOpenFlow.Contracts;

namespace BimOpenFlow.Host.Api;

/// <summary>Which stored graph-input values apply to a document, and how inputs are reported.</summary>
public static class GraphInputValues
{
    /// <summary>The stored values the document still declares and its kinds accept. A stale or
    /// invalid stored value is ignored, so editing a document never makes an analysis unopenable.</summary>
    public static IReadOnlyDictionary<string, string> Held(GraphDocument doc, IReadOnlyDictionary<string, string> stored)
        => stored
            .Where(kv => doc.Inputs.FirstOrDefault(i => i.Name == kv.Key) is { } input && input.CheckValue(kv.Value) is null)
            .ToDictionary(kv => kv.Key, kv => kv.Value);

    /// <summary>Each declared input with its declaration and its value from <paramref name="values"/>, else its default.</summary>
    public static IReadOnlyList<GraphInputInfo> Describe(GraphDocument doc, IReadOnlyDictionary<string, string> values)
        => doc.Inputs
            .OrderBy(i => i.Name, StringComparer.Ordinal)
            .Select(i => new GraphInputInfo(i.Name, i.Kind.ByName<ParamKind>(), i.Default, i.Description,
                values.GetValueOrDefault(i.Name) ?? i.Default))
            .ToList();
}
