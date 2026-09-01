using System.Globalization;
using Ara3D.DataTable;

namespace BimOpenFlow.Nodes.Tables;

/// <summary>Pack-specific table helpers: canonical cell text for key
/// comparison and row selection; the shared column/ordinal machinery lives
/// in BimOpenFlow.Nodes.Support.TableColumns.</summary>
internal static class TableOps
{
    /// <summary>Canonical trimmed invariant text of a cell for key comparison;
    /// null for absent values (null keys never match).</summary>
    public static string? CanonicalText(object? value)
        => value switch
        {
            null => null,
            string s => s.Trim(),
            bool b => b ? "true" : "false",
            float f => ((double)f).ToString("R", CultureInfo.InvariantCulture),
            double d => d.ToString("R", CultureInfo.InvariantCulture),
            sbyte or byte or short or ushort or int or uint or long or ulong or decimal
                => ((IFormattable)value).ToString(null, CultureInfo.InvariantCulture),
            _ => value.ToString()?.Trim(),
        };

    /// <summary>All non-null canonical key texts in one column.</summary>
    public static HashSet<string> KeySet(this IDataTable table, int column)
    {
        var keys = new HashSet<string>();
        for (var row = 0; row < table.RowCount(); row++)
            if (CanonicalText(table[column, row]) is { } key)
                keys.Add(key);
        return keys;
    }

    public static IDataTable SelectRows(this IDataTable table, IReadOnlyList<int> rows, string name)
    {
        var builder = new DataTableBuilder(name);
        foreach (var c in table.Columns)
        {
            var values = new object?[rows.Count];
            for (var i = 0; i < rows.Count; i++)
                values[i] = table[c.ColumnIndex, rows[i]];
            builder.AddColumn(values, c.Descriptor.Name, c.Descriptor.Type);
        }
        return builder.Build();
    }
}
