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
        Inputs: [new PortSpec("source", PortType.Text, Optional: true)],
        Outputs: [new PortSpec("table", PortType.Table)],
        Params:
        [
            // Retained for old saved graphs; new graphs supply duck.source instead.
            new ParamSpec("path", ParamKind.FilePath, Control: new ParamControl("hidden")),
            new ParamSpec("sql", ParamKind.Text),
        ],
        "Runs read-only SQL against a connected shared DuckDB source.");

    public IReadOnlyList<FlowValue> Eval(IEvalContext context, IReadOnlyList<FlowValue> inputs, ParamValues parameters)
    {
        var validated = parameters.ReadOnlySql(Kind);
        if (inputs.Count > 0 && inputs[0] is TextValue source)
            return [new TableValue((cache ?? DuckSourceCache.Shared).Query(source.Value, validated))];
        var path = parameters.RequiredText("path", Kind);
        if (!File.Exists(path))
            throw new FileNotFoundException($"{Kind}: file not found: {path}", path);
        using var conn = DuckDbOps.OpenReadOnly(path);
        return [new TableValue(conn.Query(validated, "query").NormalizeDatesToText())];
    }
}
