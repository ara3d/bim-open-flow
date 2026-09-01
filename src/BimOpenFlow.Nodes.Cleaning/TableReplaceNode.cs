using Ara3D.BimOpenSchema.DuckDb;
using Ara3D.DataFlowEngine.Abstractions;

namespace BimOpenFlow.Nodes.Cleaning;

/// <summary>Replaces values in one text column by exact match, substring, or
/// regular expression, optionally case-insensitive.</summary>
public sealed class TableReplaceNode : IFlowNode
{
    public const string Kind = "table.replace";

    public NodeSpec Spec { get; } = new(
        Kind, 1, NodeCapability.Pure,
        Inputs: [new PortSpec("table", PortType.Table)],
        Outputs: [new PortSpec("table", PortType.Table)],
        Params:
        [
            new ParamSpec("column", ParamKind.Text),
            new ParamSpec("find", ParamKind.Text),
            new ParamSpec("replaceWith", ParamKind.Text),
            new ParamSpec("match", ParamKind.Enum, "exact", ["exact", "substring", "regex"]),
            new ParamSpec("caseSensitive", ParamKind.Boolean, "true"),
        ],
        "Replaces 'find' with 'replaceWith' in a text column, by exact/substring/regex match.");

    public IReadOnlyList<FlowValue> Eval(IEvalContext context,
        IReadOnlyList<FlowValue> inputs, ParamValues parameters)
    {
        var table = inputs.TableInput(0, Kind);
        var column = table.RequireTextColumn(parameters.RequiredText("column", Kind), Kind);
        var find = parameters.GetText("find");
        var replaceWith = parameters.GetText("replaceWith");
        var match = parameters.RequiredEnum("match", Kind, "exact", "exact", "substring", "regex");
        var caseSensitive = parameters.GetBoolean("caseSensitive", true);
        var c = DuckTableSql.QuoteIdent(column.Descriptor.Name);
        var flags = caseSensitive ? "g" : "gi";
        var replaced = match switch
        {
            "substring" when caseSensitive =>
                $"replace({c}, {DuckTableSql.QuoteLiteral(find)}, {DuckTableSql.QuoteLiteral(replaceWith)})",
            "substring" =>
                $"regexp_replace({c}, {DuckTableSql.QuoteLiteral(EscapeRegex(find))}, " +
                $"{DuckTableSql.QuoteLiteral(replaceWith.Replace(@"\", @"\\"))}, 'gi')",
            "regex" =>
                $"regexp_replace({c}, {DuckTableSql.QuoteLiteral(find)}, " +
                $"{DuckTableSql.QuoteLiteral(replaceWith)}, '{flags}')",
            _ when caseSensitive =>
                $"CASE WHEN {c} = {DuckTableSql.QuoteLiteral(find)} " +
                $"THEN {DuckTableSql.QuoteLiteral(replaceWith)} ELSE {c} END",
            _ =>
                $"CASE WHEN lower({c}) = lower({DuckTableSql.QuoteLiteral(find)}) " +
                $"THEN {DuckTableSql.QuoteLiteral(replaceWith)} ELSE {c} END",
        };
        var ordinal = table.OrdinalName();
        var select = string.Join(", ", table.Columns.Select(col =>
            col.Descriptor.Name == column.Descriptor.Name
                ? $"{replaced} AS {c}"
                : DuckTableSql.QuoteIdent(col.Descriptor.Name)));
        return [new TableValue(DuckTableSql.Run(Kind, table.WithOrdinal(ordinal),
            $"SELECT {select} FROM t ORDER BY {DuckTableSql.QuoteIdent(ordinal)}"))];
    }

    private static string EscapeRegex(string text)
        => string.Concat(text.Select(ch =>
            @"\^$.|?*+()[]{}".Contains(ch) ? $@"\{ch}" : ch.ToString()));
}
