using Ara3D.DataFlowEngine;
using Ara3D.DataFlowEngine.Abstractions;
using Ara3D.NodeGraph;

namespace BimOpenFlow.GraphText;

/// <summary>The comment block under a binding: the node's status and its upstream cause,
/// its warnings, and one digest per output.</summary>
public static class ResultText
{
    public const string Lead = "  // ";
    public const string Indent = "  ";

    public static IReadOnlyList<string> Lines(GraphDocument doc, GraphNode node, EvalSnapshot snapshot,
        INodeRegistry registry, GraphTextOptions options)
    {
        if (!snapshot.Results.TryGetValue(node.Id, out var result))
            return [Lead + "not evaluated"];
        var spec = registry.Find(node.Kind, node.Version)?.Spec;
        var outputs = Outputs(node, result, spec, doc, snapshot, registry, options);
        var status = Status(doc, node, result, spec, snapshot)
            + (options.Mode == GraphTextMode.Debug ? $"  exec {result.ExecutionCount}" : "");
        var warnings = result.Warnings.Select(w => Indent + "warn " + Literals.OneLine(w));
        IReadOnlyList<string> body = outputs.Count == 1
            ? [status + "  " + outputs[0].Digest.Summary, .. warnings, .. outputs[0].Digest.Details.Select(d => Indent + d)]
            : [status, .. warnings, .. outputs.SelectMany(o => Output(o.Port, o.Digest))];
        return body.Select(line => Lead + line).ToList();
    }

    private static IReadOnlyList<string> Output(string port, Digest digest)
        => [$"{Indent}{port}: {digest.Summary}", .. digest.Details.Select(d => Indent + Indent + d)];

    /// <summary>The status word, then for anything but Ok the reason the engine gives, naming
    /// the upstream node a blocked result waits on.</summary>
    public static string Status(GraphDocument doc, GraphNode node, NodeResult result, NodeSpec? spec, EvalSnapshot snapshot)
        => result.Status switch
        {
            NodeStatus.Ok => "Ok",
            NodeStatus.Error => $"Error  {Literals.OneLine(result.Error ?? "")}",
            NodeStatus.EffectPending => "EffectPending  not executed outside a Run",
            NodeStatus.Unavailable => $"Unavailable  blocked by {Cause(result.BlockingNodeId, snapshot)}",
            NodeStatus.Unready when result.BlockingNodeId is { } origin => $"Unready  waits on {Cause(origin, snapshot)}",
            NodeStatus.Unready => $"Unready  {Unconnected(doc, node, spec)}",
            _ => result.Status.ToString(),
        };

    private static string Cause(string? nodeId, EvalSnapshot snapshot)
        => nodeId is null ? "an upstream node"
            : snapshot.Results.TryGetValue(nodeId, out var origin) ? $"{nodeId} ({origin.Status})"
            : nodeId;

    private static string Unconnected(GraphDocument doc, GraphNode node, NodeSpec? spec)
    {
        var incoming = Bindings.Incoming(doc, node.Id);
        var missing = (spec?.Inputs ?? []).Where(p => !p.Optional && !incoming.ContainsKey(p.Name)).Select(p => p.Name).ToList();
        return missing.Count > 0 ? $"input {string.Join(", ", missing)} not connected" : "an input is not connected";
    }

    private static IReadOnlyList<(string Port, Digest Digest)> Outputs(GraphNode node, NodeResult result, NodeSpec? spec,
        GraphDocument doc, EvalSnapshot snapshot, INodeRegistry registry, GraphTextOptions options)
    {
        var inputs = InputValues(doc, node, snapshot, registry);
        return result.Outputs
            .Select((value, i) =>
            {
                var port = spec is not null && i < spec.Outputs.Count ? spec.Outputs[i].Name : $"out{i}";
                var digest = ValueText.Of(value, new(node, port, result.Outputs, inputs, options));
                return (port, options.Mode == GraphTextMode.Debug && i < result.OutputHashes.Count
                    ? digest with { Details = [.. digest.Details, $"hash {result.OutputHashes[i]}"] }
                    : digest);
            })
            .ToList();
    }

    /// <summary>The values wired into the node from upstream nodes that evaluated Ok.</summary>
    private static IReadOnlyList<FlowValue> InputValues(GraphDocument doc, GraphNode node, EvalSnapshot snapshot, INodeRegistry registry)
    {
        var values = new List<FlowValue>();
        foreach (var from in Bindings.Incoming(doc, node.Id).Values)
            if (doc.FindNode(from.NodeId) is { } upstream
                && snapshot.Results.GetValueOrDefault(from.NodeId) is { Status: NodeStatus.Ok } result
                && OutputIndex(registry.Find(upstream.Kind, upstream.Version)?.Spec, from.Port) is var i and >= 0
                && i < result.Outputs.Count)
                values.Add(result.Outputs[i]);
        return values;
    }

    private static int OutputIndex(NodeSpec? spec, string port)
        => spec is null ? -1 : spec.Outputs.Select(p => p.Name).ToList().IndexOf(port);
}
