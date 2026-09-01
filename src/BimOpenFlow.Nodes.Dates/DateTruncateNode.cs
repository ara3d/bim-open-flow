using Ara3D.DataFlowEngine.Abstractions;

namespace BimOpenFlow.Nodes.Dates;

/// <summary>Truncates an ISO date column to a period boundary (date_trunc):
/// the standard time-series group-by key.</summary>
public sealed class DateTruncateNode : IFlowNode
{
    public const string Kind = "date.truncate";

    private static readonly string[] Periods =
        ["year", "quarter", "month", "week", "day", "hour"];

    public NodeSpec Spec { get; } = new(
        Kind, 1, NodeCapability.Pure,
        Inputs: [new PortSpec("table", PortType.Table)],
        Outputs: [new PortSpec("table", PortType.Table)],
        Params:
        [
            new ParamSpec("column", ParamKind.Text, Suggest: SuggestSource.ColumnsOf("table")),
            new ParamSpec("period", ParamKind.Enum, "", Periods),
            new ParamSpec("name", ParamKind.Text),
        ],
        "Truncates the ISO date 'column' down to the start of its 'period' "
        + "(week starts Monday); empty 'name' replaces the column in place.");

    public IReadOnlyList<FlowValue> Eval(IEvalContext context,
        IReadOnlyList<FlowValue> inputs, ParamValues parameters)
    {
        var table = inputs.TableInput(0, Kind);
        var column = parameters.RequiredText("column", Kind);
        var period = parameters.RequiredEnum("period", Kind, "", Periods);
        var name = parameters.GetText("name");
        table.RequireColumn(column, Kind);
        table.RequireIsoDates(column, Kind);
        var expr = DateSql.IsoTextExpr($"date_trunc('{period}', {DateSql.TsExpr(column)})");
        return [new TableValue(DateSql.RunColumnExpr(table, column, name, expr, Kind))];
    }
}
