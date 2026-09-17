using System.Globalization;
using Ara3D.DataFlowEngine.Abstractions;
using Ara3D.DataTable;

namespace BimOpenFlow.Nodes.Tables;

/// <summary>Generates one ISO-8601 date column from start to end inclusive —
/// the calendar spine to left-join actuals onto so gaps show as nulls.</summary>
public sealed class TableCalendarNode : IFlowNode
{
    public const string Kind = "table.calendar";

    public NodeSpec Spec { get; } = new(
        Kind, 1, NodeCapability.Pure,
        Inputs: [],
        Outputs: [new PortSpec("table", PortType.Table)],
        Params:
        [
            new ParamSpec("name", ParamKind.Text, "date"),
            new ParamSpec("start", ParamKind.DateTime),
            new ParamSpec("end", ParamKind.DateTime),
            new ParamSpec("step", ParamKind.Enum, "day", ["day", "week", "month", "quarter", "year"]),
        ],
        "Generates one ISO-8601 date column from start to end inclusive; month/quarter/year steps use calendar arithmetic.");

    public IReadOnlyList<FlowValue> Eval(IEvalContext context, IReadOnlyList<FlowValue> inputs, ParamValues parameters)
    {
        var name = parameters.GetText("name").Trim() is { Length: > 0 } n ? n : "date";
        var start = parameters.RequiredDateTime("start", Kind);
        var end = parameters.RequiredDateTime("end", Kind);
        if (end < start)
            throw new ArgumentException($"{Kind}: 'end' is before 'start'.");
        var step = parameters.RequiredEnum("step", Kind, "day", "day", "week", "month", "quarter", "year");

        var cells = new List<object?>();
        for (var date = start; date <= end; date = Next(date, step))
            cells.Add(date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        var builder = new DataTableBuilder("calendar");
        builder.AddColumn(cells.ToArray(), name, typeof(string));
        return [new TableValue(builder.Build())];
    }

    private static DateTime Next(DateTime date, string step)
        => step switch
        {
            "day" => date.AddDays(1),
            "week" => date.AddDays(7),
            "month" => date.AddMonths(1),
            "quarter" => date.AddMonths(3),
            _ => date.AddYears(1),
        };
}
