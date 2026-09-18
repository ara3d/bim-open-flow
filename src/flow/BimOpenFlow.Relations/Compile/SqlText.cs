namespace BimOpenFlow.Relations;

/// <summary>SQL quoting and the read-only check for user SQL. Kept here rather than
/// reusing the data layer's copy so the pure layers stay free of DuckDB.</summary>
public static class SqlText
{
    public static string Ident(this string name)
        => "\"" + name.Replace("\"", "\"\"") + "\"";

    public static string Literal(this string text)
        => "'" + text.Replace("'", "''") + "'";

    /// <summary>Accepts one SELECT or WITH statement and returns it without a trailing semicolon.</summary>
    public static string RequireReadOnly(string sql)
    {
        if (string.IsNullOrWhiteSpace(sql))
            throw new ArgumentException("A SQL query is required.", nameof(sql));
        var trimmed = sql.Trim().TrimEnd(';').Trim();
        if (trimmed.Contains(';'))
            throw new ArgumentException("Only one statement is allowed per query.", nameof(sql));
        if (!StartsWithWord(trimmed, "select") && !StartsWithWord(trimmed, "with"))
            throw new ArgumentException("Only SELECT and WITH queries are allowed.", nameof(sql));
        return trimmed;
    }

    private static bool StartsWithWord(string text, string word)
        => text.StartsWith(word, StringComparison.OrdinalIgnoreCase)
           && (text.Length == word.Length || !char.IsLetterOrDigit(text[word.Length]));

    public static string ToSqlType(this ColumnType type)
        => type switch
        {
            ColumnType.Boolean => "BOOLEAN",
            ColumnType.Integer => "BIGINT",
            ColumnType.Number => "DOUBLE",
            ColumnType.Text => "VARCHAR",
            ColumnType.Date => "DATE",
            ColumnType.Timestamp => "TIMESTAMP",
            ColumnType.Binary => "BLOB",
            _ => throw new ArgumentException($"Cannot cast to {type}.", nameof(type)),
        };
}
