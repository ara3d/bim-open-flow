namespace BimOpenFlow.Nodes.Relations;

/// <summary>A Table wire value entering the pack as a relation: the rows are registered with
/// the runtime under their content hash, and the plan carries that hash and the name the
/// compiled SQL reads them under.</summary>
public sealed class RelFromTableNode(RelationRuntime runtime) : IFlowNode
{
    public const string Kind = "rel.fromTable";
    public NodeSpec Spec { get; } = new(Kind, 1, NodeCapability.Pure,
        Inputs: [new PortSpec("table", PortType.Table)], Outputs: [new PortSpec("relation", PortType.Relation)],
        Params: [new ParamSpec("name", ParamKind.Text, "t")],
        "Makes the input table available to rel.* nodes as a relation, keyed by the table's content.");

    public IReadOnlyList<FlowValue> Eval(IEvalContext context, IReadOnlyList<FlowValue> inputs, ParamValues parameters)
        => [runtime.Relation(runtime.Inline(inputs.TableInput(0, Kind), TableName(parameters)), Kind)];

    private static string TableName(ParamValues parameters)
        => parameters.GetText("name", "t").Trim() is { Length: > 0 } name
            ? name
            : throw new ArgumentException($"{Kind}: parameter 'name' cannot be blank.");
}
