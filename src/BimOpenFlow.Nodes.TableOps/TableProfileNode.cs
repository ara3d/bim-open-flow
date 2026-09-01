using System.Globalization;
using Ara3D.BimOpenSchema.DuckDb;
using Ara3D.DataFlowEngine.Abstractions;
using Ara3D.DataTable;

namespace BimOpenFlow.Nodes.TableOps;

/// <summary>Per-column statistics via DuckDB SUMMARIZE, projected to a fixed
/// column set so engine upgrades cannot change the output shape. min/max are
/// text (lexical for text columns); mean is null for non-numeric columns.</summary>
public sealed class TableProfileNode : IFlowNode
{
    public const string Kind = "table.profile";

    public NodeSpec Spec { get; } = new(
        Kind, 1, NodeCapability.Pure,
        Inputs: [new PortSpec("table", PortType.Table)],
        Outputs: [new PortSpec("profile", PortType.Table)],
        Params: [],
        "Profiles every column: type, counts, distinct count, min, max, and mean.");

    public IReadOnlyList<FlowValue> Eval(IEvalContext context,
        IReadOnlyList<FlowValue> inputs, ParamValues parameters)
    {
        var table = inputs.TableInput(0, Kind);
        var summary = TableColumns.RunSql(Kind, table, "SUMMARIZE t");
        int Col(string name) => summary.RequireColumn(name, Kind);
        var (nameCol, typeCol) = (Col("column_name"), Col("column_type"));
        var (countCol, distinctCol) = (Col("count"), Col("approx_unique"));
        var (minCol, maxCol, avgCol) = (Col("min"), Col("max"), Col("avg"));

        var rows = summary.RowCount();
        var columns = new object?[rows];
        var types = new object?[rows];
        var counts = new object?[rows];
        var nullCounts = new object?[rows];
        var distincts = new object?[rows];
        var mins = new object?[rows];
        var maxes = new object?[rows];
        var means = new object?[rows];
        for (var row = 0; row < rows; row++)
        {
            var columnName = TableColumns.CellText(summary[nameCol, row])!;
            columns[row] = columnName;
            types[row] = WireType(TableColumns.CellText(summary[typeCol, row]) ?? "");
            counts[row] = Convert.ToInt64(summary[countCol, row], CultureInfo.InvariantCulture);
            nullCounts[row] = CountNulls(table, columnName);
            distincts[row] = summary[distinctCol, row] is null or DBNull
                ? null
                : Convert.ToInt64(summary[distinctCol, row], CultureInfo.InvariantCulture);
            mins[row] = TableColumns.CellText(summary[minCol, row]);
            maxes[row] = TableColumns.CellText(summary[maxCol, row]);
            means[row] = Mean(summary[avgCol, row]);
        }

        var builder = new DataTableBuilder("profile");
        builder.AddColumn(columns, "column", typeof(string));
        builder.AddColumn(types, "type", typeof(string));
        builder.AddColumn(counts, "count", typeof(long));
        builder.AddColumn(nullCounts, "nullCount", typeof(long));
        builder.AddColumn(distincts, "distinctCount", typeof(long));
        builder.AddColumn(mins, "min", typeof(string));
        builder.AddColumn(maxes, "max", typeof(string));
        builder.AddColumn(means, "mean", typeof(double));
        return [new TableValue(builder.Build())];
    }

    /// <summary>Exact null count from the input table (SUMMARIZE only reports a
    /// rounded percentage).</summary>
    private static long CountNulls(IDataTable table, string columnName)
    {
        var col = table.RequireColumn(columnName, Kind);
        var nulls = 0L;
        for (var row = 0; row < table.RowCount(); row++)
            if (table[col, row] is null or DBNull)
                nulls++;
        return nulls;
    }

    private static object? Mean(object? avg)
        => avg switch
        {
            null or DBNull => null,
            double d => d,
            float f => (double)f,
            decimal m => (double)m,
            string s => double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var v)
                ? v : null,
            _ => Convert.ToDouble(avg, CultureInfo.InvariantCulture),
        };

    private static string WireType(string duckType)
    {
        var t = duckType.ToUpperInvariant();
        if (t == "BOOLEAN") return "Boolean";
        if (t.Contains("INT")) return "Integer";
        if (t is "FLOAT" or "DOUBLE" or "REAL" || t.StartsWith("DECIMAL")) return "Number";
        return "Text";
    }
}
