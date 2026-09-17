using System.Globalization;
using System.Text;
using Ara3D.DataTable;

namespace BimOpenFlow.Nodes.Effects;

// TODO: Ara3D.BimOpenSchema.IO.DataTableExportUtils has a near-identical CSV writer;
// unify once a shared table-IO home exists (referencing that project would drag in Sqlite).

/// <summary>RFC-4180 CSV: CRLF row terminators, header row, invariant cell formatting, quotes only when needed.</summary>
internal static class CsvWriting
{
    public static string ToCsvText(IDataTable table, string delimiter = ",", bool header = true)
    {
        var sb = new StringBuilder();
        if (header)
            AppendRow(sb, delimiter, table.Columns.Count, c => table.Columns[c].Descriptor.Name);
        for (var r = 0; r < table.Rows.Count; r++)
        {
            var row = r;
            AppendRow(sb, delimiter, table.Columns.Count, c => FormatCell(table[c, row]));
        }
        return sb.ToString();
    }

    private static void AppendRow(StringBuilder sb, string delimiter, int count, Func<int, string> cell)
    {
        for (var c = 0; c < count; c++)
        {
            if (c > 0)
                sb.Append(delimiter);
            sb.Append(Escape(cell(c), delimiter));
        }
        sb.Append("\r\n");
    }

    /// <summary>Null is empty; booleans are lowercase; numbers use invariant round-trip formatting.</summary>
    public static string FormatCell(object? value)
        => value switch
        {
            null => "",
            bool b => b ? "true" : "false",
            IFormattable f => f.ToString(null, CultureInfo.InvariantCulture) ?? "",
            _ => value.ToString() ?? "",
        };

    public static string Escape(string cell, string delimiter = ",")
        => cell.AsSpan().IndexOfAny("\"\r\n") >= 0 || cell.Contains(delimiter)
            ? $"\"{cell.Replace("\"", "\"\"")}\""
            : cell;
}
