using Ara3D.BimOpenSchema.DuckDb;
using Ara3D.DataFlowEngine.Abstractions;

namespace BimOpenFlow.Nodes.TableOps;

/// <summary>LIMIT/OFFSET over the table's deterministic order: top-N after
/// table.sort, paging through inspection.</summary>
public sealed class TableLimitNode : IFlowNode
{
    public const string Kind = "table.limit";

    public NodeSpec Spec { get; } = new(
        Kind, 1, NodeCapability.Pure,
        Inputs: [new PortSpec("table", PortType.Table)],
        Outputs: [new PortSpec("table", PortType.Table)],
        Params:
        [
            new ParamSpec("count", ParamKind.Integer),
            new ParamSpec("offset", ParamKind.Integer, "0"),
        ],
        "Keeps 'count' rows starting at 'offset' in the table's order.");

    public IReadOnlyList<FlowValue> Eval(IEvalContext context,
        IReadOnlyList<FlowValue> inputs, ParamValues parameters)
    {
        var table = inputs.TableInput(0, Kind);
        var count = parameters.GetInteger("count", -1);
        var offset = parameters.GetInteger("offset");
        if (count < 0)
            throw new ArgumentException($"{Kind}: parameter 'count' must be a non-negative integer.");
        if (offset < 0)
            throw new ArgumentException($"{Kind}: parameter 'offset' must be a non-negative integer.");
        return [new TableValue(DuckTableSql.Run(Kind, table, $"SELECT * FROM t LIMIT {count} OFFSET {offset}"))];
    }
}
