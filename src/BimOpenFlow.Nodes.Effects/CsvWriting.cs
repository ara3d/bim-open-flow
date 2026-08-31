using System.Globalization;
using System.Text;
using Ara3D.DataTable;

namespace BimOpenFlow.Nodes.Effects;

// TODO: Ara3D.BimOpenSchema.IO.DataTableExportUtils has a near-identical CSV writer;
// unify once a shared table-IO home exists (referencing that project would drag in Sqlite).

/// <summary>RFC-4180 CSV: CRLF row terminators, header row, invariant cell formatting, quotes only when needed.</summary>
internal static class CsvWriting
{
    public static string ToCsvText(IDataTable table)
    {
        var sb = new StringBuilder();
        for (var c = 0; c < table.Columns.Count; c++)
        {
            if (c > 0)
                sb.Append(',');
            sb.Append(Escape(table.Columns[c].Descriptor.Name));
        }
        sb.Append("\r\n");
        for (var r = 0; r < table.Rows.Count; r++)
        {
            for (var c = 0; c < table.Columns.Count; c++)
            {
                if (c > 0)
                    sb.Append(',');
                sb.Append(Escape(FormatCell(table[c, r])));
            }
            sb.Append("\r\n");
        }
        return sb.ToString();
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

    public static string Escape(string cell)
        => cell.AsSpan().IndexOfAny("\",\r\n") >= 0
            ? $"\"{cell.Replace("\"", "\"\"")}\""
            : cell;
}
