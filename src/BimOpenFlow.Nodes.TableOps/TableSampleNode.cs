using System.Globalization;
using Ara3D.BimOpenSchema.DuckDb;
using Ara3D.DataFlowEngine.Abstractions;

namespace BimOpenFlow.Nodes.TableOps;

/// <summary>Seeded deterministic sampling: a fixed row count (reservoir) or a
/// per-row probability (bernoulli). Sampled rows keep their input order.</summary>
public sealed class TableSampleNode : IFlowNode
{
    public const string Kind = "table.sample";

    public NodeSpec Spec { get; } = new(
        Kind, 1, NodeCapability.Pure,
        Inputs: [new PortSpec("table", PortType.Table)],
        Outputs: [new PortSpec("table", PortType.Table)],
        Params:
        [
            new ParamSpec("mode", ParamKind.Enum, "rows", ["rows", "fraction"]),
            new ParamSpec("rows", ParamKind.Integer, "100"),
            new ParamSpec("fraction", ParamKind.Number, "0.1"),
            new ParamSpec("seed", ParamKind.Integer, "1"),
        ],
        "Takes a seeded random sample: a fixed number of rows, or a fraction of them.");

    public IReadOnlyList<FlowValue> Eval(IEvalContext context,
        IReadOnlyList<FlowValue> inputs, ParamValues parameters)
    {
        var table = inputs.TableInput(0, Kind);
        var mode = parameters.RequiredEnum("mode", Kind, "rows", "rows", "fraction");
        var seed = parameters.GetInteger("seed", 1);
        string sample;
        if (mode == "rows")
        {
            var rows = parameters.GetInteger("rows", 100);
            if (rows < 0)
                throw new ArgumentException($"{Kind}: parameter 'rows' must be a non-negative integer.");
            sample = $"reservoir({rows} ROWS)";
        }
        else
        {
            var fraction = parameters.GetNumber("fraction", 0.1);
            if (fraction is < 0 or > 1 || double.IsNaN(fraction))
                throw new ArgumentException($"{Kind}: parameter 'fraction' must be between 0 and 1.");
            var percent = (fraction * 100).ToString("R", CultureInfo.InvariantCulture);
            sample = $"bernoulli({percent}%)";
        }
        var ord = TableColumns.FreeName("__row__", table);
        var cols = string.Join(", ", table.Names().Select(TableColumns.Ident));
        var sql = $"""
            SELECT {cols} FROM (
              SELECT * FROM t USING SAMPLE {sample} REPEATABLE ({seed}))
            ORDER BY {ord.Ident()}
            """;
        return [new TableValue(TableColumns.RunSql(Kind, table.WithOrdinal(ord), sql))];
    }
}
