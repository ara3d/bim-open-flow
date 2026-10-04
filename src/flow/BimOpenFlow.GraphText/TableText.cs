using System.Security.Cryptography;
using System.Text;
using Ara3D.DataTable;
using BimOpenFlow.Nodes.Support;

namespace BimOpenFlow.GraphText;

/// <summary>The generic parts of a table digest: shape, columns, sample rows, content hash.</summary>
public static class TableText
{
    public const int ShortHashLength = 12;

    /// <summary>Numbers in the content hash keep nine significant digits: enough that any
    /// change a person would call a change shows, few enough that a parallel sum's last bit does not.</summary>
    public const string HashNumberFormat = "G9";

    public static string Shape(IDataTable table)
        => $"{table.RowCount()} rows x {table.Columns.Count} cols";

    public static string Columns(IDataTable table, GraphTextOptions options)
    {
        var names = table.Columns
            .Select(c => $"{c.Descriptor.Name}:{TableColumns.KindName(c.Descriptor.Type)}")
            .ToList();
        var shown = string.Join(", ", names.Take(options.MaxColumns));
        return names.Count <= options.MaxColumns
            ? $"columns {shown}"
            : $"columns {shown} (+{names.Count - options.MaxColumns} more)";
    }

    public static IReadOnlyList<string> SampleRows(IDataTable table, GraphTextOptions options)
        => Enumerable.Range(0, Math.Min(options.SampleRows, table.RowCount()))
            .Select(row => $"[{row}] {Row(table, row, options)}")
            .ToList();

    public static string Row(IDataTable table, int row, GraphTextOptions options)
    {
        var cells = Enumerable.Range(0, Math.Min(table.Columns.Count, options.MaxColumns))
            .Select(column => Literals.Cell(table[column, row], options.Mode));
        return string.Join(", ", cells) + (table.Columns.Count > options.MaxColumns ? ", ..." : "");
    }

    /// <summary>First twelve hex digits of SHA-256 over the column names, kinds, and every
    /// cell in row order, text cells scrubbed of aliased paths.</summary>
    public static string ContentHash(IDataTable table, GraphTextOptions options)
    {
        var text = new StringBuilder();
        text.AppendJoin('\t', table.Columns.Select(c => $"{c.Descriptor.Name}:{TableColumns.KindName(c.Descriptor.Type)}"));
        for (var row = 0; row < table.RowCount(); row++)
        {
            text.Append('\n');
            for (var column = 0; column < table.Columns.Count; column++)
            {
                if (column > 0)
                    text.Append('\t');
                text.Append(CanonicalCell(table[column, row], options));
            }
        }
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(text.ToString()));
        return Convert.ToHexString(hash).ToLowerInvariant()[..ShortHashLength];
    }

    private static string CanonicalCell(object? cell, GraphTextOptions options)
        => cell switch
        {
            null or DBNull => "~",
            string s => Literals.QuoteLine(options.Scrub(s)),
            char c => Literals.QuoteLine(c.ToString()),
            float f => Literals.Number(f, HashNumberFormat),
            double d => Literals.Number(d, HashNumberFormat),
            decimal m => Literals.Number((double)m, HashNumberFormat),
            _ => TableColumns.CellText(cell) ?? "~",
        };

    /// <summary>The column's cells as numbers; null where a cell is absent or not numeric.</summary>
    public static double?[] Numbers(IDataTable table, int column)
    {
        var numbers = new double?[table.RowCount()];
        for (var row = 0; row < numbers.Length; row++)
            numbers[row] = TableColumns.CellNumber(table[column, row]);
        return numbers;
    }

    public static bool HasColumns(IDataTable table, params string[] names)
        => names.All(name => table.ColumnIndex(name) >= 0);
}
