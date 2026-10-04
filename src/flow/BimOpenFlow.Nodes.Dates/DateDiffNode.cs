using Ara3D.DataFlowEngine.Abstractions;

namespace BimOpenFlow.Nodes.Dates;

/// <summary>Counts unit boundaries between two ISO date columns
/// (date_diff), negative when 'b' is earlier than 'a'.</summary>
public sealed class DateDiffNode : IFlowNode
{
    public const string Kind = "date.diff";

    private static readonly IReadOnlyDictionary<string, string> Units =
        new Dictionary<string, string>
        {
            ["years"] = "year",
            ["months"] = "month",
            ["days"] = "day",
            ["hours"] = "hour",
            ["minutes"] = "minute",
            ["seconds"] = "second",
        };

    public NodeSpec Spec { get; } = new(
        Kind, 1, NodeCapability.Pure,
        Inputs: [new PortSpec("table", PortType.Table)],
        Outputs: [new PortSpec("table", PortType.Table)],
        Params:
        [
            new ParamSpec("a", ParamKind.Text),
            new ParamSpec("b", ParamKind.Text),
            new ParamSpec("unit", ParamKind.Enum, "days", Units.Keys.ToList()),
            new ParamSpec("name", ParamKind.Text),
        ],
        "Adds new Integer column 'name' counting 'unit' boundaries from ISO date "
        + "column 'a' to column 'b' (negative when b is earlier).");

    public IReadOnlyList<FlowValue> Eval(IEvalContext context,
        IReadOnlyList<FlowValue> inputs, ParamValues parameters)
    {
        var table = inputs.TableInput(0, Kind);
        var a = parameters.RequiredText("a", Kind);
        var b = parameters.RequiredText("b", Kind);
        var unit = parameters.RequiredEnum("unit", Kind, "days", Units.Keys.ToArray());
        var name = parameters.RequiredText("name", Kind);
        table.RequireColumn(a, Kind);
        table.RequireColumn(b, Kind);
        table.RequireIsoDates(a, Kind);
        table.RequireIsoDates(b, Kind);
        var expr = $"CAST(date_diff('{Units[unit]}', {DateSql.TsExpr(a)}, {DateSql.TsExpr(b)}) AS BIGINT)";
        return [new TableValue(DateSql.RunColumnExpr(table, a, name, expr, Kind))];
    }
}
