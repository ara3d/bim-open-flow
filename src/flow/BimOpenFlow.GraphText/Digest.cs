using Ara3D.DataFlowEngine.Abstractions;
using Ara3D.NodeGraph;

namespace BimOpenFlow.GraphText;

/// <summary>What one output value prints: a one-line summary and indented detail lines.</summary>
public sealed record Digest(string Summary, IReadOnlyList<string> Details);

/// <summary>What a digest may look at besides its own value: the node that produced it, the
/// node's other outputs (view3d.color's legend beside its instances), and the values wired
/// into the node (the instance table before an isolate). Never pixels, never files.</summary>
public sealed record DigestContext(
    GraphNode Node,
    string Port,
    IReadOnlyList<FlowValue> Outputs,
    IReadOnlyList<FlowValue> Inputs,
    GraphTextOptions Options)
{
    public GraphTextMode Mode
        => Options.Mode;

    public string Number(double value)
        => Literals.Number(value, Mode);

    /// <summary>Up to MaxListed items joined with ", ", then "+k more".</summary>
    public string List(IReadOnlyList<string> items)
        => items.Count <= Options.MaxListed
            ? string.Join(", ", items)
            : string.Join(", ", items.Take(Options.MaxListed)) + $" (+{items.Count - Options.MaxListed} more)";
}
