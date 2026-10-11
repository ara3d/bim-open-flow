using Ara3D.DataFlowEngine.Abstractions;

namespace BimOpenFlow.Nodes.Value;

/// <summary>A constant Integer value.</summary>
public sealed class IntegerNode : IFlowNode
{
    public const string Kind = "value.integer";
    public NodeSpec Spec { get; } = new(Kind, 1, NodeCapability.Pure,
        Inputs: [], Outputs: [new PortSpec("value", PortType.Integer)],
        Params: [new ParamSpec("value", ParamKind.Integer, "0")],
        "Holds one Integer for a value that several nodes share through their parameter sockets. Wire its output into a bindable parameter, or edit the integer here once.");

    public IReadOnlyList<FlowValue> Eval(IEvalContext context, IReadOnlyList<FlowValue> inputs, ParamValues parameters)
        => [new IntegerValue(parameters.GetInteger("value"))];
}
