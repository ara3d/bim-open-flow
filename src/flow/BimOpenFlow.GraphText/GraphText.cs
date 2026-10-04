using Ara3D.DataFlowEngine;
using Ara3D.DataFlowEngine.Abstractions;
using Ara3D.NodeGraph;

namespace BimOpenFlow.GraphText;

/// <summary>Prints a graph document, and optionally its evaluation, as graph text: a
/// <c>dfg</c> version line, a header comment with the analysis id and short graph hash, then
/// one binding per node in evaluation order, each followed by its result as comments.
/// The grammar and the digests are described in docs/graph-text.md.</summary>
public static class GraphText
{
    public const int ShortGraphHashLength = 12;

    /// <summary>The document is printed and hashed as given (placeholders intact); results are
    /// read from the snapshot by node id, so the snapshot may come from a copy whose paths were
    /// rewritten for evaluation. Every line passes through the options' path aliases.</summary>
    public static string Print(GraphDocument doc, EvalSnapshot? snapshot, INodeRegistry registry, GraphTextOptions? options = null)
    {
        options ??= GraphTextOptions.Golden;
        var lines = new List<string> { $"dfg {GraphFormat.Version};", Header(doc, options) };
        foreach (var node in Bindings.Order(doc))
        {
            lines.Add(Bindings.Line(doc, node, registry.Find(node.Kind, node.Version)?.Spec));
            if (snapshot is not null)
                lines.AddRange(ResultText.Lines(doc, node, snapshot, registry, options));
        }
        return options.Scrub(string.Join("\n", lines) + "\n");
    }

    public static string Header(GraphDocument doc, GraphTextOptions options)
    {
        var hash = $"graph {doc.ComputeGraphHash()[..ShortGraphHashLength]}";
        var mode = options.Mode == GraphTextMode.Debug ? "   debug" : "";
        return options.AnalysisId is { } id ? $"// {id}   {hash}{mode}" : $"// {hash}{mode}";
    }
}
