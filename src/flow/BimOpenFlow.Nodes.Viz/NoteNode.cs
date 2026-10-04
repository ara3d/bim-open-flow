using Ara3D.DataFlowEngine.Abstractions;

namespace BimOpenFlow.Nodes.Viz;

/// <summary>A comment pinned to the canvas — no ports, no computation, just
/// text a person leaves for whoever opens the graph next.</summary>
public sealed class NoteNode : IFlowNode
{
    public const string Kind = "view.note";

    public NodeSpec Spec { get; } = new(
        Kind, 1, NodeCapability.Pure,
        Inputs: [],
        Outputs: [],
        Params: [new ParamSpec("text", ParamKind.Text)],
        "A comment on the canvas; computes nothing and has no ports.");

    public IReadOnlyList<FlowValue> Eval(IEvalContext context,
        IReadOnlyList<FlowValue> inputs, ParamValues parameters)
        => [];
}
