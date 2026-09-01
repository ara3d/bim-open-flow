using Ara3D.BimOpenSchema.DuckDb;
using Ara3D.DataFlowEngine.Abstractions;

namespace BimOpenFlow.Nodes.TableOps;

/// <summary>Adds one window-function column — rankings, lag/lead, running totals,
/// moving averages, share-of-total — leaving the input rows and order unchanged.</summary>
public sealed class TableWindowNode : IFlowNode
{
    public const string Kind = "table.window";

    private static readonly string[] Functions =
        ["rowNumber", "rank", "denseRank", "lag", "lead", "cumSum", "movingAvg", "percentOfTotal"];

    public NodeSpec Spec { get; } = new(
        Kind, 1, NodeCapability.Pure,
        Inputs: [new PortSpec("table", PortType.Table)],
        Outputs: [new PortSpec("table", PortType.Table)],
        Params:
        [
            new ParamSpec("function", ParamKind.Enum, "", Functions),
            new ParamSpec("column", ParamKind.Text),
            new ParamSpec("partitionBy", ParamKind.Text),
            new ParamSpec("orderBy", ParamKind.Text),
            new ParamSpec("offset", ParamKind.Integer, "1"),
            new ParamSpec("windowSize", ParamKind.Integer, "3"),
            new ParamSpec("name", ParamKind.Text),
        ],
        "Adds one window-function column: ranking, lag/lead, cumulative sum, moving average, or percent of total.");

    public IReadOnlyList<FlowValue> Eval(IEvalContext context,
        IReadOnlyList<FlowValue> inputs, ParamValues parameters)
    {
        var table = inputs.TableInput(0, Kind);
        var function = parameters.RequiredEnum("function", Kind, "", Functions);
        var name = parameters.RequiredText("name", Kind);
        if (table.ColumnIndex(name) >= 0)
            throw new ArgumentException($"{Kind}: column '{name}' already exists.");

        var ranking = function is "rowNumber" or "rank" or "denseRank";
        var column = ranking ? "" : table.CanonicalName(parameters.RequiredText("column", Kind), Kind);
        var partitionBy = parameters.GetText("partitionBy").SplitNames()
            .Select(n => table.CanonicalName(n, Kind).Ident()).ToList();
        var ord = TableColumns.FreeName("__row__", table);
        var userOrder = table.SortTerms(parameters.GetText("orderBy"), Kind);
        var fullOrder = string.Join(", ", userOrder.Append($"{ord.Ident()} ASC"));
        var rankOrder = userOrder.Count > 0 ? string.Join(", ", userOrder) : $"{ord.Ident()} ASC";
        var partition = partitionBy.Count > 0 ? $"PARTITION BY {string.Join(", ", partitionBy)} " : "";

        var offset = parameters.GetInteger("offset", 1);
        if (offset < 0)
            throw new ArgumentException($"{Kind}: parameter 'offset' must be a non-negative integer.");
        var windowSize = parameters.GetInteger("windowSize", 3);
        if (windowSize < 1)
            throw new ArgumentException($"{Kind}: parameter 'windowSize' must be at least 1.");

        var expr = function switch
        {
            "rowNumber" => $"row_number() OVER ({partition}ORDER BY {fullOrder})",
            "rank" => $"rank() OVER ({partition}ORDER BY {rankOrder})",
            "denseRank" => $"dense_rank() OVER ({partition}ORDER BY {rankOrder})",
            "lag" => $"lag({column.Ident()}, {offset}) OVER ({partition}ORDER BY {fullOrder})",
            "lead" => $"lead({column.Ident()}, {offset}) OVER ({partition}ORDER BY {fullOrder})",
            // sum over BIGINT yields HUGEINT, which the wire cannot carry
            "cumSum" => WrapInteger(table, column,
                $"sum({column.Ident()}) OVER ({partition}ORDER BY {fullOrder} " +
                "ROWS BETWEEN UNBOUNDED PRECEDING AND CURRENT ROW)"),
            "movingAvg" => $"avg({column.Ident()}) OVER ({partition}ORDER BY {fullOrder} " +
                           $"ROWS BETWEEN {windowSize - 1} PRECEDING AND CURRENT ROW)",
            _ => $"CAST({column.Ident()} AS DOUBLE) / sum({column.Ident()}) OVER ({partition.TrimEnd()})",
        };
        var cols = string.Join(", ", table.Names().Select(TableColumns.Ident));
        var sql = $"SELECT {cols}, {expr} AS {name.Ident()} FROM t ORDER BY {ord.Ident()}";
        return [new TableValue(TableColumns.RunSql(Kind, table.WithOrdinal(ord), sql))];
    }

    private static string WrapInteger(Ara3D.DataTable.IDataTable table, string column, string expr)
        => TableColumns.KindName(table.Columns[table.RequireColumn(column, Kind)].Descriptor.Type) == "Integer"
            ? $"CAST({expr} AS BIGINT)"
            : expr;
}
