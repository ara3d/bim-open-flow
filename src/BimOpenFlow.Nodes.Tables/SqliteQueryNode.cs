using Ara3D.DataFlowEngine.Abstractions;

namespace BimOpenFlow.Nodes.Tables;

/// <summary>Opens a SQLite database file read-only and runs one validated
/// SELECT/WITH query against it.</summary>
public sealed class SqliteQueryNode : IFlowNode
{
    public const string Kind = "sqlite.query";

    public NodeSpec Spec { get; } = new(
        Kind, 1, NodeCapability.Pure,
        Inputs: [],
        Outputs: [new PortSpec("table", PortType.Table)],
        Params:
        [
            new ParamSpec("path", ParamKind.FilePath),
            new ParamSpec("sql", ParamKind.Text),
        ],
        "Runs one read-only SQL query against a SQLite database file.");

    public IReadOnlyList<FlowValue> Eval(IEvalContext context, IReadOnlyList<FlowValue> inputs, ParamValues parameters)
        => throw new NotImplementedException("track B");
}
