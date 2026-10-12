using Ara3D.BimOpenSchema.DuckDb;
using Ara3D.DataFlowEngine.Abstractions;
using SchemaRules = BimOpenFlow.Relations.SchemaRules;
using SchemaInputs = BimOpenFlow.Relations.SchemaInputs;
using WindowFunction = BimOpenFlow.Relations.WindowFunction;

namespace BimOpenFlow.Nodes.TableOps;

/// <summary>Adds one window-function column — rankings, lag/lead, running totals,
/// moving averages, share-of-total — leaving the input rows and order unchanged.</summary>
public sealed class TableWindowNode : ISchemaNode
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
            new ParamSpec("column", ParamKind.Text, Suggest: SuggestSource.ColumnsOf("table")),
            new ParamSpec("partitionBy", ParamKind.Text, Suggest: SuggestSource.ColumnsOf("table"), List: true),
            new ParamSpec("orderBy", ParamKind.Text, Suggest: SuggestSource.ColumnsOf("table"), List: true),
            new ParamSpec("offset", ParamKind.Integer, "1"),
            new ParamSpec("windowSize", ParamKind.Integer, "3"),
            new ParamSpec("name", ParamKind.Text),
        ],
        "Adds one window-function column: ranking, lag/lead, cumulative sum, moving average, or percent of total.",
        Schemas: [SchemaSpec.Inferred("table")]);

    /// <summary>The parameters, checked; evaluation and the shape rule both read them here.</summary>
    private readonly record struct Args(string Function, string Name, string? Column, IReadOnlyList<string> PartitionBy,
        IReadOnlyList<string> OrderColumns, long Offset, long WindowSize)
    {
        public bool Ranking => Function is "rowNumber" or "rank" or "denseRank";

        public static Args Read(ParamValues parameters)
        {
            var function = parameters.RequiredEnum("function", Kind, "", Functions);
            var offset = parameters.GetInteger("offset", 1);
            if (offset < 0)
                throw new ArgumentException($"{Kind}: parameter 'offset' must be a non-negative integer.");
            var windowSize = parameters.GetInteger("windowSize", 3);
            if (windowSize < 1)
                throw new ArgumentException($"{Kind}: parameter 'windowSize' must be at least 1.");
            var ranking = function is "rowNumber" or "rank" or "denseRank";
            return new Args(function, parameters.RequiredText("name", Kind),
                ranking ? null : parameters.RequiredText("column", Kind),
                parameters.GetText("partitionBy").SplitNames(),
                SortSyntax.ParseKeys(parameters.GetText("orderBy"), Kind).Select(k => k.Column).ToList(),
                offset, windowSize);
        }
    }

    public SchemaResult Infer(string output, IReadOnlyList<SchemaInput> inputs, ParamValues parameters, ISchemaCatalog catalog)
        => SchemaInputs.Run(Spec, inputs, schemas =>
        {
            var args = Args.Read(parameters);
            return SchemaRules.Window(schemas[0], args.Name, Enum.Parse<WindowFunction>(args.Function, ignoreCase: true),
                args.Column, args.PartitionBy, args.OrderColumns);
        }, throughDuckDb: true);

    public IReadOnlyList<FlowValue> Eval(IEvalContext context,
        IReadOnlyList<FlowValue> inputs, ParamValues parameters)
    {
        var table = inputs.TableInput(0, Kind);
        var (function, name, columnParameter, partitionNames, _, offset, windowSize) = Args.Read(parameters);
        if (table.ColumnIndex(name) >= 0)
            throw new ArgumentException($"{Kind}: column '{name}' already exists.");

        var column = columnParameter is null ? "" : table.CanonicalName(columnParameter, Kind);
        var partitionBy = partitionNames.Select(n => table.CanonicalName(n, Kind).Ident()).ToList();
        var ord = TableColumns.FreeName("__row__", table);
        var userOrder = table.SortTerms(parameters.GetText("orderBy"), Kind);
        var fullOrder = string.Join(", ", userOrder.Append($"{ord.Ident()} ASC"));
        var rankOrder = userOrder.Count > 0 ? string.Join(", ", userOrder) : $"{ord.Ident()} ASC";
        var partition = partitionBy.Count > 0 ? $"PARTITION BY {string.Join(", ", partitionBy)} " : "";

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
        var cols = string.Join(", ", table.Names().Select(DuckTableSql.QuoteIdent));
        var sql = $"SELECT {cols}, {expr} AS {name.Ident()} FROM t ORDER BY {ord.Ident()}";
        return [new TableValue(DuckTableSql.Run(Kind, table.WithOrdinal(ord), sql))];
    }

    private static string WrapInteger(Ara3D.DataTable.IDataTable table, string column, string expr)
        => TableColumns.KindName(table.Columns[table.RequireColumn(column, Kind)].Descriptor.Type) == "Integer"
            ? $"CAST({expr} AS BIGINT)"
            : expr;
}
