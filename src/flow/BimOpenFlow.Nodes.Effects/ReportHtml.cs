using System.Text;
using Ara3D.DataTable;

namespace BimOpenFlow.Nodes.Effects;

// TODO: replace with BimOpenFlow.Publishing when it lands.

/// <summary>Minimal standalone HTML report: a title plus the table rendered as plain HTML.</summary>
internal static class ReportHtml
{
    public static string ToHtml(string title, IDataTable table)
    {
        var sb = new StringBuilder();
        sb.Append("<!doctype html>\n<html>\n<head>\n<meta charset=\"utf-8\">\n<title>")
            .Append(Escape(title)).Append("</title>\n</head>\n<body>\n<h1>")
            .Append(Escape(title)).Append("</h1>\n<table>\n<thead><tr>");
        foreach (var column in table.Columns)
            sb.Append("<th>").Append(Escape(column.Descriptor.Name)).Append("</th>");
        sb.Append("</tr></thead>\n<tbody>\n");
        for (var r = 0; r < table.Rows.Count; r++)
        {
            sb.Append("<tr>");
            for (var c = 0; c < table.Columns.Count; c++)
                sb.Append("<td>").Append(Escape(CsvWriting.FormatCell(table[c, r]))).Append("</td>");
            sb.Append("</tr>\n");
        }
        return sb.Append("</tbody>\n</table>\n</body>\n</html>\n").ToString();
    }

    public static string Escape(string value)
        => value
            .Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;")
            .Replace("\"", "&quot;");
}
