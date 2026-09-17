using System;
using System.Globalization;
using System.Text;
using Ara3D.DataTable;
using BimOpenFlow.Contracts;

namespace BimOpenFlow.Publishing;

/// <summary>
/// Renders an IDataTable as a plain, script-free HTML table: escaped cells,
/// invariant formatting, numeric columns right-aligned, rows capped with a
/// "showing N of M" note. Used by the static report generator.
/// </summary>
public static class HtmlTables
{
    public const int DefaultMaxRows = 100;

    public static string ToHtml(this IDataTable table, int maxRows = DefaultMaxRows)
    {
        var columns = table.Columns;
        var total = columns.Count == 0 ? 0 : columns[0].Count;
        var shown = Math.Min(total, maxRows);

        var sb = new StringBuilder();
        sb.Append("<table class=\"bof-table\">\n<thead>\n<tr>");
        foreach (var c in columns)
            sb.Append($"<th>{Html.Escape(c.Descriptor.Name)}</th>");
        sb.Append("</tr>\n</thead>\n<tbody>\n");
        for (var r = 0; r < shown; r++)
        {
            sb.Append("<tr>");
            foreach (var c in columns)
            {
                var numeric = IsNumeric(c.Descriptor.Type);
                sb.Append(numeric ? "<td class=\"bof-num\">" : "<td>");
                sb.Append(Html.Escape(FormatCell(c[r])));
                sb.Append("</td>");
            }
            sb.Append("</tr>\n");
        }
        sb.Append("</tbody>\n</table>\n");
        if (shown < total)
            sb.Append($"<p class=\"bof-table-note\">Showing {shown} of {total} rows</p>\n");
        return sb.ToString();
    }

    public static string FormatCell(object? cell)
        => cell switch
        {
            null or DBNull => "",
            bool b => b ? "true" : "false",
            double d => d.ToString("R", CultureInfo.InvariantCulture),
            float f => f.ToString("R", CultureInfo.InvariantCulture),
            IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
            _ => cell.ToString() ?? "",
        };

    private static bool IsNumeric(Type type)
        => TableJson.TryToColumnType(type, out var t)
           && t is ColumnType.Integer or ColumnType.Number;
}
