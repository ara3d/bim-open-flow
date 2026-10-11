using Ara3D.BimOpenSchema.DuckDb;
using Ara3D.DataFlowEngine.Abstractions;

namespace BimOpenFlow.Nodes.DuckDb;

/// <summary>Opens an existing .duckdb database read-only and runs one validated
/// SELECT/WITH query against it.</summary>
public sealed class DuckQueryNode(DuckSourceCache? cache = null) : IFlowNode
{
    public const string Kind = "duck.query";

    public NodeSpec Spec { get; } = new(
        Kind, 1, NodeCapability.Pure,
        Inputs: [],
        Outputs: [new PortSpec("table", PortType.Table)],
        Params:
        [
            DuckSourceInput.Spec,
            new ParamSpec("sql", ParamKind.Text),
        ],
        "Runs read-only SQL against a DuckDB file: type the path, or wire a duck.source into path.");

    public IReadOnlyList<FlowValue> Eval(IEvalContext context, IReadOnlyList<FlowValue> inputs, ParamValues parameters)
    {
        var validated = parameters.ReadOnlySql(Kind);
        if (DuckSourceInput.Wired(parameters) is { } source)
            return [new TableValue((cache ?? DuckSourceCache.Shared).Query(source.Path, validated))];
        var path = parameters.RequiredText("path", Kind);
        if (!File.Exists(path))
            throw new FileNotFoundException($"{Kind}: file not found: {path}", path);
        using var conn = DuckDbOps.OpenReadOnly(path);
        return [new TableValue(conn.Query(validated, "query").NormalizeDatesToText())];
    }
}
