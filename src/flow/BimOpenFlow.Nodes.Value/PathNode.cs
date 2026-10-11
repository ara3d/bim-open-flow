using Ara3D.DataFlowEngine.Abstractions;

namespace BimOpenFlow.Nodes.Value;

/// <summary>A constant file path, emitted as Text.</summary>
public sealed class PathNode : IFlowNode
{
    public const string Kind = "value.path";
    public NodeSpec Spec { get; } = new(Kind, 1, NodeCapability.Pure,
        Inputs: [], Outputs: [new PortSpec("path", PortType.Text)],
        Params: [new ParamSpec("path", ParamKind.FilePath, Suggest: SuggestSource.FilesWith())],
        "Holds one file path for a value that several nodes share through their parameter sockets, such as a model file read by many nodes. Pick the file once and wire the output into each bindable path parameter.");

    public IReadOnlyList<FlowValue> Eval(IEvalContext context, IReadOnlyList<FlowValue> inputs, ParamValues parameters)
        => [new TextValue(parameters.GetText("path"))];
}
