namespace BimOpenFlow.Nodes.Relations;

/// <summary>A CSV file under a registered folder source.</summary>
public sealed class RelCsvNode(RelationRuntime runtime) : IFlowNode
{
    public const string Kind = "rel.csv";
    public NodeSpec Spec { get; } = new(Kind, 1, NodeCapability.Pure,
        Inputs: [], Outputs: [new PortSpec("relation", PortType.Relation)],
        Params: [new ParamSpec("source", ParamKind.Text), new ParamSpec("path", ParamKind.Text)],
        "A CSV file, named by its registered source folder and relative path. Nothing is read until a node is inspected.");

    public IReadOnlyList<FlowValue> Eval(IEvalContext context, IReadOnlyList<FlowValue> inputs, ParamValues parameters)
        => [runtime.Relation(Plans.Csv(parameters.RequiredText("source", Kind), parameters.RequiredText("path", Kind)), Kind)];
}

/// <summary>A table in a registered database source.</summary>
public sealed class RelTableNode(RelationRuntime runtime) : IFlowNode
{
    public const string Kind = "rel.table";
    public NodeSpec Spec { get; } = new(Kind, 1, NodeCapability.Pure,
        Inputs: [], Outputs: [new PortSpec("relation", PortType.Relation)],
        Params: [new ParamSpec("source", ParamKind.Text), new ParamSpec("table", ParamKind.Text)],
        "A table in a registered database source. Nothing is read until a node is inspected.");

    public IReadOnlyList<FlowValue> Eval(IEvalContext context, IReadOnlyList<FlowValue> inputs, ParamValues parameters)
        => [runtime.Relation(Plans.Table(parameters.RequiredText("source", Kind), parameters.RequiredText("table", Kind)), Kind)];
}

/// <summary>User SQL over up to three relations, visible as t1, t2, and t3.</summary>
public sealed class RelSqlNode(RelationRuntime runtime) : IFlowNode
{
    public const string Kind = "rel.sql";
    public NodeSpec Spec { get; } = new(Kind, 1, NodeCapability.Pure,
        Inputs: [new PortSpec("t1", PortType.Relation), new PortSpec("t2", PortType.Relation, Optional: true), new PortSpec("t3", PortType.Relation, Optional: true)],
        Outputs: [new PortSpec("relation", PortType.Relation)],
        Params: [new ParamSpec("sql", ParamKind.Text)],
        "One read-only SELECT over the connected relations, which it sees as t1, t2, and t3.");

    public IReadOnlyList<FlowValue> Eval(IEvalContext context, IReadOnlyList<FlowValue> inputs, ParamValues parameters)
    {
        var plans = new[] { inputs.PlanInput(0, Kind), inputs.OptionalPlanInput(1, Kind), inputs.OptionalPlanInput(2, Kind) }
            .TakeWhile(p => p is not null).Select(p => p!).ToArray();
        try
        {
            return [runtime.Relation(Plans.Sql(parameters.RequiredText("sql", Kind), plans), Kind)];
        }
        catch (ArgumentException e) when (!e.Message.StartsWith(Kind, StringComparison.Ordinal))
        {
            throw new ArgumentException($"{Kind}: {e.Message}", e);
        }
    }
}
