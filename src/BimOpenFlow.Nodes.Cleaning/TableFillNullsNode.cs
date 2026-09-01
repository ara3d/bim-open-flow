using System.Globalization;
using Ara3D.BimOpenSchema.DuckDb;
using Ara3D.DataFlowEngine.Abstractions;
using Ara3D.DataTable;

namespace BimOpenFlow.Nodes.Cleaning;

/// <summary>Fills nulls in the named columns with a typed constant, or the nearest
/// earlier (forward) or later (backward) non-null value in the table's row order,
/// resetting at 'partitionBy' boundaries.</summary>
public sealed class TableFillNullsNode : IFlowNode
{
    public const string Kind = "table.fillNulls";

    public NodeSpec Spec { get; } = new(
        Kind, 1, NodeCapability.Pure,
        Inputs: [new PortSpec("table", PortType.Table)],
        Outputs: [new PortSpec("table", PortType.Table)],
        Params:
        [
            new ParamSpec("columns", ParamKind.Text),
            new ParamSpec("strategy", ParamKind.Enum, "constant", ["constant", "forward", "backward"]),
            new ParamSpec("value", ParamKind.Text),
            new ParamSpec("partitionBy", ParamKind.Text),
        ],
        "Fills nulls in 'columns' with a constant, or the nearest earlier/later non-null value in row order.");

    public IReadOnlyList<FlowValue> Eval(IEvalContext context,
        IReadOnlyList<FlowValue> inputs, ParamValues parameters)
    {
        var table = inputs.TableInput(0, Kind);
        var targets = parameters.RequiredText("columns", Kind).SplitNames()
            .Select(n => table.RequireColumn(n, Kind)).ToList();
        var strategy = parameters.RequiredEnum("strategy", Kind, "constant", "constant", "forward", "backward");
        var partitions = parameters.GetText("partitionBy").SplitNames()
            .Select(n => DuckTableSql.QuoteIdent(table.RequireColumn(n, Kind).Descriptor.Name)).ToList();
        var ordinal = table.OrdinalName();
        var targetNames = targets.Select(c => c.Descriptor.Name).ToHashSet(StringComparer.Ordinal);
        var select = string.Join(", ", table.Columns.Select(c =>
            targetNames.Contains(c.Descriptor.Name)
                ? $"{Filled(c)} AS {DuckTableSql.QuoteIdent(c.Descriptor.Name)}"
                : DuckTableSql.QuoteIdent(c.Descriptor.Name)));
        return [new TableValue(table.WithOrdinal(ordinal).RunSql(
            $"SELECT {select} FROM t ORDER BY {DuckTableSql.QuoteIdent(ordinal)}", Kind))];

        string Filled(IDataColumn column)
        {
            var c = DuckTableSql.QuoteIdent(column.Descriptor.Name);
            var partition = partitions.Count > 0 ? $"PARTITION BY {string.Join(", ", partitions)} " : "";
            var order = $"ORDER BY {DuckTableSql.QuoteIdent(ordinal)}";
            return strategy switch
            {
                "forward" =>
                    $"last_value({c} IGNORE NULLS) OVER ({partition}{order} ROWS BETWEEN UNBOUNDED PRECEDING AND CURRENT ROW)",
                "backward" =>
                    $"first_value({c} IGNORE NULLS) OVER ({partition}{order} ROWS BETWEEN CURRENT ROW AND UNBOUNDED FOLLOWING)",
                _ => $"coalesce({c}, {ConstantLiteral(column)})",
            };
        }

        string ConstantLiteral(IDataColumn column)
        {
            var value = parameters.GetText("value");
            var type = column.Descriptor.Type;
            if (type == typeof(string))
                return DuckTableSql.QuoteLiteral(value);
            if (type == typeof(long) || type == typeof(int))
                return long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var i)
                    ? i.ToString(CultureInfo.InvariantCulture)
                    : throw CastError(column, value);
            if (type == typeof(double) || type == typeof(float))
                return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var d)
                    ? d.ToString("R", CultureInfo.InvariantCulture)
                    : throw CastError(column, value);
            if (type == typeof(bool))
                return bool.TryParse(value, out var b)
                    ? b ? "TRUE" : "FALSE"
                    : throw CastError(column, value);
            throw new ArgumentException(
                $"{Kind}: column '{column.Descriptor.Name}' has unsupported type '{type.Name}' for a constant fill.");
        }

        static ArgumentException CastError(IDataColumn column, string value)
            => new($"{Kind}: value '{value}' cannot be cast to the type of column '{column.Descriptor.Name}'.");
    }
}
