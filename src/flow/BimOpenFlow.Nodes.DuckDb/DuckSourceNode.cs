using Ara3D.DataFlowEngine.Abstractions;

namespace BimOpenFlow.Nodes.DuckDb;

/// <summary>Loads a shared database source and emits it as a Database value identified by the file's
/// content hash. Branch its source output into duck.query, duck.table and duck.tables.</summary>
public sealed class DuckSourceNode(DuckSourceCache? cache = null) : IFlowNode
{
    public const string Kind = "duck.source";
    public NodeSpec Spec { get; } = new(Kind, 1, NodeCapability.Pure,
        Inputs: [], Outputs: [new PortSpec("source", PortType.Database)],
        Params: [new ParamSpec("path", ParamKind.FilePath, Suggest: SuggestSource.FilesWith(".duckdb"))],
        "Opens one shared read-only DuckDB source for connected query branches.");

    public IReadOnlyList<FlowValue> Eval(IEvalContext context, IReadOnlyList<FlowValue> inputs, ParamValues parameters)
        => [(cache ?? DuckSourceCache.Shared).Load(parameters.RequiredText("path", Kind))];
}
