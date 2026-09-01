using System.Text;
using Ara3D.DataFlowEngine.Abstractions;

namespace BimOpenFlow.NodeDocs;

/// <summary>One pack of nodes as the generator sees it: a display name, a one-line
/// intro, and the nodes in registration order.</summary>
public sealed record Pack(string Name, string Intro, IReadOnlyList<IFlowNode> Nodes);

/// <summary>Renders the full nodes.md text (LF line endings, no timestamps) from
/// the preamble, the per-node notes, and the packs' NodeSpecs.</summary>
public static class MarkdownEmitter
{
    public static string Render(IReadOnlyList<Pack> packs)
    {
        var sb = new StringBuilder();
        sb.Append(Preamble.Text.ReplaceLineEndings("\n"));
        sb.Append("\n\n## Packs\n\n");
        AppendIndex(sb, packs);
        foreach (var pack in packs)
            AppendPack(sb, pack);
        return sb.ToString();
    }

    private static void AppendIndex(StringBuilder sb, IReadOnlyList<Pack> packs)
    {
        sb.Append("| Pack | Nodes | Kinds |\n|---|---|---|\n");
        foreach (var pack in packs)
        {
            var kinds = string.Join(", ", pack.Nodes.Select(n => $"`{n.Spec.Kind}`"));
            sb.Append($"| {pack.Name} | {pack.Nodes.Count} | {kinds} |\n");
        }
    }

    private static void AppendPack(StringBuilder sb, Pack pack)
    {
        sb.Append($"\n## {pack.Name}\n\n{pack.Intro}\n");
        foreach (var node in pack.Nodes)
            AppendNode(sb, node.Spec);
    }

    private static void AppendNode(StringBuilder sb, NodeSpec spec)
    {
        sb.Append($"\n### `{spec.Kind}` (v{spec.Version}) — {spec.Capability}\n\n");
        sb.Append($"{spec.Description}\n");
        if (NodeNotes.For(spec.Kind) is { } note)
            sb.Append($"\n{note}\n");

        AppendPorts(sb, "Inputs", spec.Inputs, withOptional: true);
        AppendPorts(sb, "Outputs", spec.Outputs, withOptional: false);
        AppendParams(sb, spec.Params);
    }

    private static void AppendPorts(StringBuilder sb, string title, IReadOnlyList<PortSpec> ports, bool withOptional)
    {
        sb.Append($"\n**{title}**");
        if (ports.Count == 0)
        {
            sb.Append(": none\n");
            return;
        }
        sb.Append(withOptional
            ? "\n\n| Name | Type | Required |\n|---|---|---|\n"
            : "\n\n| Name | Type |\n|---|---|\n");
        foreach (var port in ports)
            sb.Append(withOptional
                ? $"| `{port.Name}` | {port.Type} | {(port.Optional ? "optional" : "required")} |\n"
                : $"| `{port.Name}` | {port.Type} |\n");
    }

    private static void AppendParams(StringBuilder sb, IReadOnlyList<ParamSpec> parameters)
    {
        sb.Append("\n**Params**");
        if (parameters.Count == 0)
        {
            sb.Append(": none\n");
            return;
        }
        sb.Append("\n\n| Name | Kind | Default | Allowed values |\n|---|---|---|---|\n");
        foreach (var p in parameters)
        {
            var @default = p.Default.Length == 0 ? "—" : $"`{p.Default}`";
            var allowed = p.EnumValues is { Count: > 0 } values
                ? string.Join(", ", values.Select(v => $"`{v}`"))
                : "—";
            sb.Append($"| `{p.Name}` | {p.Kind} | {@default} | {allowed} |\n");
        }
    }
}
