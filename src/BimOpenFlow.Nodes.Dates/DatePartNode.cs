using Ara3D.DataFlowEngine.Abstractions;

namespace BimOpenFlow.Nodes.Dates;

/// <summary>Extracts one integer component (year, month, ISO day-of-week, ...)
/// from an ISO date column into a new Integer column.</summary>
public sealed class DatePartNode : IFlowNode
{
    public const string Kind = "date.part";

    private static readonly IReadOnlyDictionary<string, string> Parts =
        new Dictionary<string, string>
        {
            ["year"] = "year",
            ["quarter"] = "quarter",
            ["month"] = "month",
            ["week"] = "week",
            ["dayOfMonth"] = "day",
            ["dayOfWeek"] = "isodow",
            ["dayOfYear"] = "dayofyear",
            ["hour"] = "hour",
            ["minute"] = "minute",
            ["second"] = "second",
        };

    public NodeSpec Spec { get; } = new(
        Kind, 1, NodeCapability.Pure,
        Inputs: [new PortSpec("table", PortType.Table)],
        Outputs: [new PortSpec("table", PortType.Table)],
        Params:
        [
            new ParamSpec("column", ParamKind.Text, Suggest: SuggestSource.ColumnsOf("table")),
            new ParamSpec("part", ParamKind.Enum, "", Parts.Keys.ToList()),
            new ParamSpec("name", ParamKind.Text),
        ],
        "Adds the integer 'part' of the ISO date 'column' as new column 'name'; "
        + "dayOfWeek is ISO (Monday = 1).");

    public IReadOnlyList<FlowValue> Eval(IEvalContext context,
        IReadOnlyList<FlowValue> inputs, ParamValues parameters)
    {
        var table = inputs.TableInput(0, Kind);
        var column = parameters.RequiredText("column", Kind);
        var part = parameters.RequiredEnum("part", Kind, "", Parts.Keys.ToArray());
        var name = parameters.RequiredText("name", Kind);
        table.RequireColumn(column, Kind);
        table.RequireIsoDates(column, Kind);
        var expr = $"CAST(date_part('{Parts[part]}', {DateSql.TsExpr(column)}) AS BIGINT)";
        return [new TableValue(DateSql.RunColumnExpr(table, column, name, expr, Kind))];
    }
}
