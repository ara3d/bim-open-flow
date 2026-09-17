using Ara3D.DataFlowEngine.Abstractions;

namespace BimOpenFlow.Nodes.DuckDb;

/// <summary>Loads a shared database source. Branch its source output into query nodes.</summary>
public sealed class DuckSourceNode(DuckSourceCache? cache = null) : IFlowNode
{
    public const string Kind = "duck.source";
    public NodeSpec Spec { get; } = new(Kind, 1, NodeCapability.Pure,
        Inputs: [], Outputs: [new PortSpec("source", PortType.Text)],
        Params: [new ParamSpec("path", ParamKind.FilePath)],
        "Opens one shared read-only DuckDB source for connected query branches.");

    public IReadOnlyList<FlowValue> Eval(IEvalContext context, IReadOnlyList<FlowValue> inputs, ParamValues parameters)
        => [new TextValue((cache ?? DuckSourceCache.Shared).Load(parameters.RequiredText("path", Kind)))];
}
