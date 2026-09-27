using System.Globalization;
using System.Text;

namespace BimOpenFlow.GraphText;

/// <summary>How strings, numbers, and table cells are spelled in graph text.</summary>
public static class Literals
{
    public const int MaxCellText = 48;
    public const int MaxScalarText = 200;
    private const string TripleQuote = "\"\"\"";

    /// <summary>A multi-line value (SQL) prints raw between triple quotes on lines of its own;
    /// anything else, or text that cannot be written raw, prints escaped on one line.</summary>
    public static string Quote(string value)
        => value.Contains('\n') && !value.Contains('\r') && !value.Contains(TripleQuote)
            ? $"{TripleQuote}\n{value}\n{TripleQuote}"
            : QuoteLine(value);

    public static string QuoteLine(string value)
    {
        var text = new StringBuilder("\"");
        foreach (var c in value)
            text.Append(c switch
            {
                '"' => "\\\"",
                '\\' => "\\\\",
                '\n' => "\\n",
                '\r' => "\\r",
                '\t' => "\\t",
                _ when char.IsControl(c) => $"\\u{(int)c:x4}",
                _ => c.ToString(),
            });
        return text.Append('"').ToString();
    }

    /// <summary>Quoted, with "..." after the closing quote when cut.</summary>
    public static string QuoteShort(string value, int max)
        => value.Length <= max ? QuoteLine(value) : QuoteLine(value[..max]) + "...";

    /// <summary>Golden: four significant digits. Debug: round-trip precision.</summary>
    public static string Number(double value, GraphTextMode mode)
        => Number(value, mode == GraphTextMode.Golden ? "G4" : "R");

    /// <summary>Negative zero prints as 0 so a sign flip in the last bit never shows.</summary>
    public static string Number(double value, string format)
        => (value == 0 ? 0 : value).ToString(format, CultureInfo.InvariantCulture);

    public static string Cell(object? cell, GraphTextMode mode)
        => cell switch
        {
            null or DBNull => "null",
            string s => QuoteShort(s, MaxCellText),
            char c => QuoteLine(c.ToString()),
            bool b => b ? "true" : "false",
            float f => Number(f, mode),
            double d => Number(d, mode),
            decimal m => Number((double)m, mode),
            IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
            _ => QuoteShort(cell.ToString() ?? "", MaxCellText),
        };

    /// <summary>The first line of a message; the rest joined with " / " so a status stays one line.</summary>
    public static string OneLine(string message)
        => string.Join(" / ", message.Split('\n').Select(l => l.TrimEnd('\r')).Where(l => l.Length > 0));
}
