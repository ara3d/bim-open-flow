using System.Globalization;
using Ara3D.DataFlowEngine.Abstractions;

namespace BimOpenFlow.Nodes.Dates;

/// <summary>Keeps rows whose ISO date column falls in a half-open range
/// [from, to). Promoted to graph parameters, the bounds become the report's
/// date-range control. Both bounds empty = warn and pass through.</summary>
public sealed class DateFilterNode : IFlowNode
{
    public const string Kind = "date.filter";

    private static readonly string[] IsoFormats = ["yyyy-MM-dd", "yyyy-MM-ddTHH:mm:ss"];

    public NodeSpec Spec { get; } = new(
        Kind, 1, NodeCapability.Pure,
        Inputs: [new PortSpec("table", PortType.Table)],
        Outputs: [new PortSpec("table", PortType.Table)],
        Params:
        [
            new ParamSpec("column", ParamKind.Text),
            new ParamSpec("from", ParamKind.DateTime),
            new ParamSpec("to", ParamKind.DateTime),
        ],
        "Keeps rows where ISO date 'column' >= 'from' (inclusive) and < 'to' "
        + "(exclusive); an empty bound is open. Rows with a null date are dropped.");

    public IReadOnlyList<FlowValue> Eval(IEvalContext context,
        IReadOnlyList<FlowValue> inputs, ParamValues parameters)
    {
        var table = inputs.TableInput(0, Kind);
        var column = parameters.RequiredText("column", Kind);
        var from = ParseBound(parameters.GetText("from"), "from");
        var to = ParseBound(parameters.GetText("to"), "to");
        table.RequireColumn(column, Kind);
        table.RequireIsoDates(column, Kind);

        if (from == null && to == null)
        {
            context.Warn($"{Kind}: both 'from' and 'to' are empty; passing the table through unchanged.");
            return [new TableValue(table)];
        }

        var ts = DateSql.TsExpr(column);
        var bounds = new List<string>();
        if (from != null)
            bounds.Add($"{ts} >= {Literal(from.Value)}");
        if (to != null)
            bounds.Add($"{ts} < {Literal(to.Value)}");
        var where = $"WHERE {string.Join(" AND ", bounds)}";
        return [new TableValue(DateSql.RunOrdered(table, "", where, Kind))];
    }

    private static DateTime? ParseBound(string text, string paramName)
        => string.IsNullOrEmpty(text)
            ? null
            : DateTime.TryParseExact(text, IsoFormats, CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var value)
                ? value
                : throw new ArgumentException(
                    $"{Kind}: parameter '{paramName}' must be ISO-8601 ('yyyy-MM-dd' or 'yyyy-MM-ddTHH:mm:ss').");

    private static string Literal(DateTime value)
        => $"TIMESTAMP '{value.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)}'";
}
