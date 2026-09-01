using Ara3D.BimOpenSchema.DuckDb;
using Ara3D.DataFlowEngine.Abstractions;

namespace BimOpenFlow.Nodes.DuckDb;

/// <summary>Opens an existing .duckdb database read-only and runs one validated
/// SELECT/WITH query against it.</summary>
public sealed class DuckQueryNode : IFlowNode
{
    public const string Kind = "duck.query";

    public NodeSpec Spec { get; } = new(
        Kind, 1, NodeCapability.Pure,
        Inputs: [],
        Outputs: [new PortSpec("table", PortType.Table)],
        Params:
        [
            new ParamSpec("path", ParamKind.FilePath),
            new ParamSpec("sql", ParamKind.Text),
        ],
        "Runs one read-only SQL query against a .duckdb database file.");

    public IReadOnlyList<FlowValue> Eval(IEvalContext context, IReadOnlyList<FlowValue> inputs, ParamValues parameters)
    {
        var path = parameters.RequiredText("path", Kind);
        if (!File.Exists(path))
            throw new FileNotFoundException($"{Kind}: file not found: {path}", path);
        var validated = parameters.ReadOnlySql(Kind);
        using var conn = DuckDbOps.OpenReadOnly(path);
        return [new TableValue(conn.Query(validated, "query").NormalizeDatesToText())];
    }
}
