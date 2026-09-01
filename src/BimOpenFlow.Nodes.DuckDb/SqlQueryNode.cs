using Ara3D.DataFlowEngine.Abstractions;

namespace BimOpenFlow.Nodes.DuckDb;

/// <summary>SQL over one to four flowing tables. Connected inputs load into an
/// in-memory DuckDB as t1..t4 (t aliases t1); t2..t4 are optional ports, so a
/// single-table query needs only t1 connected.</summary>
public sealed class SqlQueryNode : IFlowNode
{
    public const string Kind = "sql.query";

    public NodeSpec Spec { get; } = new(
        Kind, 1, NodeCapability.Pure,
        Inputs:
        [
            new PortSpec("t1", PortType.Table),
            new PortSpec("t2", PortType.Table, Optional: true),
            new PortSpec("t3", PortType.Table, Optional: true),
            new PortSpec("t4", PortType.Table, Optional: true),
        ],
        Outputs: [new PortSpec("table", PortType.Table)],
        Params: [new ParamSpec("sql", ParamKind.Text)],
        "Runs one read-only SQL query over the connected input tables t1..t4 (t = t1).");

    public IReadOnlyList<FlowValue> Eval(IEvalContext context, IReadOnlyList<FlowValue> inputs, ParamValues parameters)
        => throw new NotImplementedException("track A");
}
