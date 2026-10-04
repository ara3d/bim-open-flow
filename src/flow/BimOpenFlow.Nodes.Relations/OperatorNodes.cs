namespace BimOpenFlow.Nodes.Relations;

/// <summary>The one-input operators share a shape: read the plan, apply one operator, validate.</summary>
public abstract class UnaryRelationNode(RelationRuntime runtime, string kind, IReadOnlyList<ParamSpec> parameters, string description) : IFlowNode
{
    public NodeSpec Spec { get; } = new(kind, 1, NodeCapability.Pure,
        Inputs: [new PortSpec("input", PortType.Relation)], Outputs: [new PortSpec("relation", PortType.Relation)],
        Params: parameters, description);

    protected abstract Plan Apply(Plan input, ParamValues parameters);

    public IReadOnlyList<FlowValue> Eval(IEvalContext context, IReadOnlyList<FlowValue> inputs, ParamValues parameters)
        => [runtime.Relation(Apply(inputs.PlanInput(0, Spec.Kind), parameters), Spec.Kind)];

    protected static ParamSpec Columns(string name)
        => new(name, ParamKind.Text, Suggest: SuggestSource.ColumnsOf("input"));
}

public sealed class RelSelectNode(RelationRuntime runtime) : UnaryRelationNode(runtime, Kind,
    [Columns("columns")], "Keeps only the comma-separated columns, in that order.")
{
    public const string Kind = "rel.select";
    protected override Plan Apply(Plan input, ParamValues parameters)
        => input.Select(parameters.RequiredText("columns", Kind).SplitNames().ToArray());
}

public sealed class RelFilterNode(RelationRuntime runtime) : UnaryRelationNode(runtime, Kind,
    [new ParamSpec("expr", ParamKind.Expression)], "Keeps rows where the Boolean expression is true.")
{
    public const string Kind = "rel.filter";
    protected override Plan Apply(Plan input, ParamValues parameters)
        => input.Filter(parameters.RequiredText("expr", Kind).ParseExpr());
}

public sealed class RelDeriveNode(RelationRuntime runtime) : UnaryRelationNode(runtime, Kind,
    [new ParamSpec("name", ParamKind.Text), new ParamSpec("expr", ParamKind.Expression)],
    "Appends a column computed from the expression.")
{
    public const string Kind = "rel.derive";
    protected override Plan Apply(Plan input, ParamValues parameters)
        => input.Derive(parameters.RequiredText("name", Kind), parameters.RequiredText("expr", Kind).ParseExpr());
}

public sealed class RelSortNode(RelationRuntime runtime) : UnaryRelationNode(runtime, Kind,
    [Columns("by")], "Sorts by comma-separated terms such as 'height desc, name'.")
{
    public const string Kind = "rel.sort";
    protected override Plan Apply(Plan input, ParamValues parameters)
        => new Sort(input, RelationArgs.ParseSortKeys(parameters.RequiredText("by", Kind), Kind));
}

public sealed class RelLimitNode(RelationRuntime runtime) : UnaryRelationNode(runtime, Kind,
    [new ParamSpec("count", ParamKind.Integer, "100"), new ParamSpec("offset", ParamKind.Integer, "0")],
    "Keeps count rows after skipping offset rows.")
{
    public const string Kind = "rel.limit";
    protected override Plan Apply(Plan input, ParamValues parameters)
        => input.Limit(parameters.GetInteger("count", 100), parameters.GetInteger("offset"));
}

public sealed class RelAggregateNode(RelationRuntime runtime) : UnaryRelationNode(runtime, Kind,
    [Columns("groupBy"), new ParamSpec("aggregates", ParamKind.Text)],
    "Groups by the comma-separated columns (may be empty) and computes 'func(column) as name' aggregates (count/sum/min/max/avg).")
{
    public const string Kind = "rel.aggregate";
    protected override Plan Apply(Plan input, ParamValues parameters)
        => new Aggregate(input, parameters.GetText("groupBy").SplitNames(),
            RelationArgs.ParseAggregations(parameters.RequiredText("aggregates", Kind), Kind));
}

public sealed class RelJoinNode(RelationRuntime runtime) : IFlowNode
{
    public const string Kind = "rel.join";
    public NodeSpec Spec { get; } = new(Kind, 1, NodeCapability.Pure,
        Inputs: [new PortSpec("left", PortType.Relation), new PortSpec("right", PortType.Relation)],
        Outputs: [new PortSpec("relation", PortType.Relation)],
        Params:
        [
            new ParamSpec("leftKey", ParamKind.Text, Suggest: SuggestSource.ColumnsOf("left")),
            new ParamSpec("rightKey", ParamKind.Text, Suggest: SuggestSource.ColumnsOf("right")),
            new ParamSpec("kind", ParamKind.Enum, "inner", ["inner", "left", "right", "full", "semi", "anti"]),
        ],
        "Joins left to right on one key each. Right columns that collide with left ones get a _right suffix.");

    public IReadOnlyList<FlowValue> Eval(IEvalContext context, IReadOnlyList<FlowValue> inputs, ParamValues parameters)
        => [runtime.Relation(inputs.PlanInput(0, Kind).Join(inputs.PlanInput(1, Kind),
            parameters.RequiredText("leftKey", Kind), parameters.RequiredText("rightKey", Kind),
            RelationArgs.ParseJoinKind(parameters.GetText("kind", "inner"), Kind)), Kind)];
}

/// <summary>The bridge to the row-based packs: runs the plan and emits an ordinary Table.</summary>
public sealed class RelMaterializeNode(RelationRuntime runtime) : IFlowNode
{
    public const string Kind = "rel.materialize";
    public NodeSpec Spec { get; } = new(Kind, 1, NodeCapability.Pure,
        Inputs: [new PortSpec("input", PortType.Relation)], Outputs: [new PortSpec("table", PortType.Table)],
        Params: [new ParamSpec("limit", ParamKind.Integer, "0")],
        "Runs the relation and outputs its rows as a Table; limit 0 means every row.");

    public IReadOnlyList<FlowValue> Eval(IEvalContext context, IReadOnlyList<FlowValue> inputs, ParamValues parameters)
    {
        var limit = parameters.GetInteger("limit");
        return [new TableValue(runtime.Materialize(inputs.PlanInput(0, Kind), limit > 0 ? limit : null))];
    }
}
