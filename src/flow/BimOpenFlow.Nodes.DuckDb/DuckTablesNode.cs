using Ara3D.BimOpenSchema.DuckDb;
using Ara3D.DataFlowEngine.Abstractions;

namespace BimOpenFlow.Nodes.DuckDb;

/// <summary>Lists the tables of a .duckdb database file (read-only) with real
/// column and row counts. Discovery: what is in this database I was handed?</summary>
public sealed class DuckTablesNode : IFlowNode
{
    public const string Kind = "duck.tables";

    public NodeSpec Spec { get; } = new(
        Kind, 1, NodeCapability.Pure,
        Inputs: [],
        Outputs: [new PortSpec("tables", PortType.Table)],
        Params: [DuckSourceInput.Spec],
        "Lists the tables in a .duckdb database file with their column and row counts.");

    public IReadOnlyList<FlowValue> Eval(IEvalContext context, IReadOnlyList<FlowValue> inputs, ParamValues parameters)
    {
        var path = DuckSourceInput.ResolvePath(parameters, Kind);
        using var conn = DuckDbOps.OpenReadOnly(path);
        return [new TableValue(DuckTableList.Of(conn))];
    }
}
