using Ara3D.DataFlowEngine.Abstractions;

namespace BimOpenFlow.Nodes.Value;

/// <summary>A constant Number value.</summary>
public sealed class NumberNode : IFlowNode
{
    public const string Kind = "value.number";
    public NodeSpec Spec { get; } = new(Kind, 1, NodeCapability.Pure,
        Inputs: [], Outputs: [new PortSpec("value", PortType.Number)],
        Params: [new ParamSpec("value", ParamKind.Number, "0")],
        "Holds one Number for a value that several nodes share through their parameter sockets. Wire its output into a bindable parameter, or edit the number here once.");

    public IReadOnlyList<FlowValue> Eval(IEvalContext context, IReadOnlyList<FlowValue> inputs, ParamValues parameters)
        => [new NumberValue(parameters.GetNumber("value"))];
}
