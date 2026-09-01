using Ara3D.BimOpenSchema.DuckDb;
using Ara3D.DataFlowEngine.Abstractions;

namespace BimOpenFlow.Nodes.Dates;

/// <summary>Parses a text column into ISO-8601 date/datetime text via
/// strptime (or a plain ISO cast when no format is given). The entry point of
/// the Dates set: every other date node requires an ISO date column.</summary>
public sealed class DateParseNode : IFlowNode
{
    public const string Kind = "date.parse";

    public NodeSpec Spec { get; } = new(
        Kind, 1, NodeCapability.Pure,
        Inputs: [new PortSpec("table", PortType.Table)],
        Outputs: [new PortSpec("table", PortType.Table)],
        Params:
        [
            new ParamSpec("column", ParamKind.Text),
            new ParamSpec("format", ParamKind.Text),
            new ParamSpec("onError", ParamKind.Enum, "error", ["error", "null"]),
            new ParamSpec("name", ParamKind.Text),
        ],
        "Parses 'column' with the strptime 'format' (empty = ISO-8601) into ISO date text; "
        + "'onError' nulls or rejects unparseable values; empty 'name' replaces the column in place.");

    public IReadOnlyList<FlowValue> Eval(IEvalContext context,
        IReadOnlyList<FlowValue> inputs, ParamValues parameters)
    {
        var table = inputs.TableInput(0, Kind);
        var column = parameters.RequiredText("column", Kind);
        var format = parameters.GetText("format");
        var onError = parameters.RequiredEnum("onError", Kind, "error", "error", "null");
        var name = parameters.GetText("name");
        table.RequireColumn(column, Kind);

        var source = $"CAST({DuckTableSql.QuoteIdent(column)} AS VARCHAR)";
        var parsed = string.IsNullOrEmpty(format)
            ? $"TRY_CAST({source} AS TIMESTAMP)"
            : $"try_strptime({source}, {DuckTableSql.QuoteLiteral(format)})";

        var failed = CountFailures(table, column, parsed);
        if (failed > 0 && onError == "error")
            throw new ArgumentException(
                $"{Kind}: {failed} value(s) in column '{column}' did not parse"
                + (string.IsNullOrEmpty(format) ? " as ISO-8601." : $" with format '{format}'."));
        if (failed > 0)
            context.Warn($"{Kind}: nulled {failed} unparseable value(s) in column '{column}'.");

        return [new TableValue(DateSql.RunColumnExpr(
            table, column, name, DateSql.IsoTextExpr(parsed), Kind))];
    }

    private static long CountFailures(Ara3D.DataTable.IDataTable table, string column, string parsed)
    {
        try
        {
            var count = DuckTableSql.Run(table,
                $"SELECT count(*) FROM t WHERE {DuckTableSql.QuoteIdent(column)} IS NOT NULL AND ({parsed}) IS NULL");
            return Convert.ToInt64(count[0, 0]);
        }
        catch (Exception e) when (e is not ArgumentException)
        {
            throw new ArgumentException($"{Kind}: parsing failed: {e.Message}");
        }
    }
}
