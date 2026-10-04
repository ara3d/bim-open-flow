using Ara3D.BimOpenSchema.DuckDb;
using Ara3D.BimOpenSchema.IO;
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
    {
        var validated = parameters.ReadOnlySql(Kind);
        using var conn = BosDuckDb.OpenInMemory();
        for (var i = 0; i < inputs.Count; i++)
        {
            if (i > 0 && inputs[i] is MissingValue)
                continue;
            if (inputs[i] is not TableValue t)
                throw new ArgumentException($"{Kind}: input t{i + 1} must be a Table.");
            conn.WriteTable(t.Table, $"t{i + 1}");
        }
        conn.Execute("CREATE VIEW t AS SELECT * FROM t1");
        return [new TableValue(conn.Query(validated, "query").NormalizeDatesToText())];
    }
}
