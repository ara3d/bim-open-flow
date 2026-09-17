using System.Globalization;
using Ara3D.DataFlowEngine.Abstractions;
using Ara3D.DataTable;

namespace BimOpenFlow.Nodes.Viz;

/// <summary>Column selection, row ordering, and projection shared by the viz
/// nodes. Everything builds new tables; input tables are never mutated.</summary>
// TODO: hoist a shared Project(table, indices, rowOrder) into Nodes.Support —
// this copy loop now exists here, in TableProjectNode (Nodes.Tables), and in
// TableColumns.WithOrdinal (Support); Bos.TableOps.KeepRows is a fourth cousin.
internal static class VizProjection
{
    public static bool IsNumeric(this IDataColumn column)
        => TableColumns.KindName(column.Descriptor.Type) is "Integer" or "Number";

    /// <summary>Indices of the named columns, in name order; unknown names
    /// warn and are skipped.</summary>
    public static List<int> ResolveColumns(IEvalContext context, IDataTable table,
        IReadOnlyList<string> names, string kind)
    {
        var indices = new List<int>();
        foreach (var name in names)
        {
            var i = table.ColumnIndex(name);
            if (i >= 0)
                indices.Add(i);
            else
                context.Warn($"{kind}: no column named '{name}'");
        }
        return indices;
    }

    /// <summary>The comma-separated value columns, or when empty every
    /// Integer/Number column except 'excluding'.</summary>
    public static List<int> ValueColumns(IEvalContext context, IDataTable table,
        string names, int excluding, string kind)
        => string.IsNullOrWhiteSpace(names)
            ? NumericColumns(table, excluding)
            : ResolveColumns(context, table, names.SplitNames(), kind);

    public static List<int> NumericColumns(IDataTable table, int excluding)
    {
        var indices = new List<int>();
        for (var i = 0; i < table.Columns.Count; i++)
            if (i != excluding && table.Columns[i].IsNumeric())
                indices.Add(i);
        return indices;
    }

    public static int FirstTextColumn(IDataTable table)
    {
        for (var i = 0; i < table.Columns.Count; i++)
            if (TableColumns.KindName(table.Columns[i].Descriptor.Type) == "Text")
                return i;
        return -1;
    }

    /// <summary>Resolves an optional column name: a named-but-absent column
    /// warns; -1 means no column.</summary>
    public static int OptionalColumn(IEvalContext context, IDataTable table,
        string name, string kind)
    {
        if (string.IsNullOrWhiteSpace(name))
            return -1;
        var i = table.ColumnIndex(name.Trim());
        if (i < 0)
            context.Warn($"{kind}: no column named '{name.Trim()}'");
        return i;
    }

    /// <summary>Row indices ordered by the given column; stable, numeric
    /// compare for Integer/Number columns, ordinal otherwise.</summary>
    public static IReadOnlyList<int> SortedRows(IDataTable table, int column, bool ascending)
    {
        var comparer = table.Columns[column].IsNumeric()
            ? NumericCellComparer.Instance
            : (IComparer<object?>)OrdinalCellComparer.Instance;
        var rows = Enumerable.Range(0, table.RowCount());
        var ordered = ascending
            ? rows.OrderBy(r => table[column, r], comparer)
            : rows.OrderByDescending(r => table[column, r], comparer);
        return ordered.ToList();
    }

    /// <summary>A new table with the given columns, rows optionally reordered.</summary>
    public static IDataTable Project(IDataTable table, IReadOnlyList<int> columns,
        IReadOnlyList<int>? rowOrder = null)
    {
        var rows = table.RowCount();
        var builder = new DataTableBuilder(table.Name);
        foreach (var col in columns)
        {
            var cells = new object?[rows];
            for (var row = 0; row < rows; row++)
                cells[row] = table[col, rowOrder is null ? row : rowOrder[row]];
            builder.AddColumn(cells, table.Columns[col].Descriptor.Name, table.Columns[col].Descriptor.Type);
        }
        return builder.Build();
    }

    private static double? CellNumber(object? cell)
        => cell switch
        {
            null or DBNull => null,
            sbyte or byte or short or ushort or int or uint or long or ulong
                or float or double or decimal
                => Convert.ToDouble(cell, CultureInfo.InvariantCulture),
            _ => double.TryParse(TableColumns.CellText(cell), NumberStyles.Float,
                CultureInfo.InvariantCulture, out var v) ? v : null,
        };

    private sealed class NumericCellComparer : IComparer<object?>
    {
        public static readonly NumericCellComparer Instance = new();
        public int Compare(object? x, object? y)
            => Comparer<double?>.Default.Compare(CellNumber(x), CellNumber(y));
    }

    private sealed class OrdinalCellComparer : IComparer<object?>
    {
        public static readonly OrdinalCellComparer Instance = new();
        public int Compare(object? x, object? y)
            => string.CompareOrdinal(TableColumns.CellText(x), TableColumns.CellText(y));
    }
}
