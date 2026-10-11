using Ara3D.DataFlowEngine.Abstractions;

namespace BimOpenFlow.Nodes.Value;

/// <summary>A constant Text value.</summary>
public sealed class TextNode : IFlowNode
{
    public const string Kind = "value.text";
    public NodeSpec Spec { get; } = new(Kind, 1, NodeCapability.Pure,
        Inputs: [], Outputs: [new PortSpec("value", PortType.Text)],
        Params: [new ParamSpec("value", ParamKind.Text)],
        "Holds one Text value for a value that several nodes share through their parameter sockets. Wire its output into a bindable parameter, or edit the text here once.");

    public IReadOnlyList<FlowValue> Eval(IEvalContext context, IReadOnlyList<FlowValue> inputs, ParamValues parameters)
        => [new TextValue(parameters.GetText("value"))];
}
