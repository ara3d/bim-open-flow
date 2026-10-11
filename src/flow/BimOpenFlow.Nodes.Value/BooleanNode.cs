using Ara3D.DataFlowEngine.Abstractions;

namespace BimOpenFlow.Nodes.Value;

/// <summary>A constant Boolean value.</summary>
public sealed class BooleanNode : IFlowNode
{
    public const string Kind = "value.boolean";
    public NodeSpec Spec { get; } = new(Kind, 1, NodeCapability.Pure,
        Inputs: [], Outputs: [new PortSpec("value", PortType.Boolean)],
        Params: [new ParamSpec("value", ParamKind.Boolean, "false")],
        "Holds one true or false for a value that several nodes share through their parameter sockets. Wire its output into a bindable parameter, or edit the flag here once.");

    public IReadOnlyList<FlowValue> Eval(IEvalContext context, IReadOnlyList<FlowValue> inputs, ParamValues parameters)
        => [new BooleanValue(parameters.GetBoolean("value"))];
}
