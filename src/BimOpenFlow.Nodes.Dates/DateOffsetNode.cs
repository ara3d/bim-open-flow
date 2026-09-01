using System.Globalization;
using Ara3D.DataFlowEngine.Abstractions;

namespace BimOpenFlow.Nodes.Dates;

/// <summary>Shifts an ISO date column by a signed interval using DuckDB's
/// calendar rules (month-end clamped): due dates, look-back windows.</summary>
public sealed class DateOffsetNode : IFlowNode
{
    public const string Kind = "date.offset";

    private static readonly IReadOnlyDictionary<string, string> Units =
        new Dictionary<string, string>
        {
            ["years"] = "YEAR",
            ["months"] = "MONTH",
            ["days"] = "DAY",
            ["hours"] = "HOUR",
            ["minutes"] = "MINUTE",
        };

    public NodeSpec Spec { get; } = new(
        Kind, 1, NodeCapability.Pure,
        Inputs: [new PortSpec("table", PortType.Table)],
        Outputs: [new PortSpec("table", PortType.Table)],
        Params:
        [
            new ParamSpec("column", ParamKind.Text),
            new ParamSpec("amount", ParamKind.Integer),
            new ParamSpec("unit", ParamKind.Enum, "days", Units.Keys.ToList()),
            new ParamSpec("name", ParamKind.Text),
        ],
        "Shifts the ISO date 'column' by 'amount' (may be negative) whole 'unit's, "
        + "calendar-aware (Jan 31 + 1 month = end of February); empty 'name' replaces in place.");

    public IReadOnlyList<FlowValue> Eval(IEvalContext context,
        IReadOnlyList<FlowValue> inputs, ParamValues parameters)
    {
        var table = inputs.TableInput(0, Kind);
        var column = parameters.RequiredText("column", Kind);
        var amountText = parameters.RequiredText("amount", Kind);
        var unit = parameters.RequiredEnum("unit", Kind, "days", Units.Keys.ToArray());
        var name = parameters.GetText("name");
        if (!long.TryParse(amountText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var amount))
            throw new ArgumentException($"{Kind}: parameter 'amount' must be an integer.");
        table.RequireColumn(column, Kind);
        table.RequireIsoDates(column, Kind);
        var expr = DateSql.IsoTextExpr(
            $"{DateSql.TsExpr(column)} + ({amount}) * INTERVAL 1 {Units[unit]}");
        return [new TableValue(DateSql.RunColumnExpr(table, column, name, expr, Kind))];
    }
}
