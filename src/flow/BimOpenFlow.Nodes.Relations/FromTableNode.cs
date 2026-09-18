namespace BimOpenFlow.Nodes.Relations;

/// <summary>A Table wire value entering the pack as a relation. Contract C4 of the
/// nrc-handoff wave; the body belongs to track C.</summary>
public sealed class RelFromTableNode(RelationRuntime runtime) : IFlowNode
{
    public const string Kind = "rel.fromTable";
    public NodeSpec Spec { get; } = new(Kind, 1, NodeCapability.Pure,
        Inputs: [new PortSpec("table", PortType.Table)], Outputs: [new PortSpec("relation", PortType.Relation)],
        Params: [new ParamSpec("name", ParamKind.Text, "t")],
        "Makes the input table available to rel.* nodes as a relation, keyed by the table's content.");

    public IReadOnlyList<FlowValue> Eval(IEvalContext context, IReadOnlyList<FlowValue> inputs, ParamValues parameters)
        => throw new NotImplementedException($"Track C fills in {Kind}.");
}
