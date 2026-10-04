namespace BimOpenFlow.Relations.DuckDb;

/// <summary>Maps DuckDB type names (as DESCRIBE and information_schema report them) and
/// CLR column types to the closed ColumnType vocabulary.</summary>
public static class DuckDbTypes
{
    public static ColumnType FromDuckDb(string typeName)
    {
        var name = typeName.ToUpperInvariant();
        var head = name.IndexOf('(') is var i && i >= 0 ? name[..i] : name;
        return head switch
        {
            "BOOLEAN" => ColumnType.Boolean,
            "TINYINT" or "SMALLINT" or "INTEGER" or "BIGINT" or "HUGEINT"
                or "UTINYINT" or "USMALLINT" or "UINTEGER" or "UBIGINT" or "UHUGEINT" => ColumnType.Integer,
            "FLOAT" or "DOUBLE" or "DECIMAL" or "REAL" => ColumnType.Number,
            "VARCHAR" or "TEXT" or "STRING" or "CHAR" or "UUID" => ColumnType.Text,
            "DATE" => ColumnType.Date,
            "TIMESTAMP" or "TIMESTAMP_S" or "TIMESTAMP_MS" or "TIMESTAMP_NS" or "TIMESTAMPTZ" or "TIMESTAMP WITH TIME ZONE" => ColumnType.Timestamp,
            "BLOB" or "BYTEA" => ColumnType.Binary,
            _ => ColumnType.Unknown,
        };
    }

    public static ColumnType FromClr(Type type)
    {
        var t = Nullable.GetUnderlyingType(type) ?? type;
        return t == typeof(bool) ? ColumnType.Boolean
            : t == typeof(sbyte) || t == typeof(byte) || t == typeof(short) || t == typeof(ushort)
              || t == typeof(int) || t == typeof(uint) || t == typeof(long) || t == typeof(ulong) || t == typeof(System.Numerics.BigInteger) ? ColumnType.Integer
            : t == typeof(float) || t == typeof(double) || t == typeof(decimal) ? ColumnType.Number
            : t == typeof(string) || t == typeof(char) || t == typeof(Guid) ? ColumnType.Text
            : t == typeof(DateOnly) ? ColumnType.Date
            : t == typeof(DateTime) || t == typeof(DateTimeOffset) ? ColumnType.Timestamp
            : t == typeof(byte[]) ? ColumnType.Binary
            : ColumnType.Unknown;
    }

    /// <summary>Whether a materialized column of the given CLR type can carry the declared
    /// type. Dates arrive as ISO text after normalization, and Unknown accepts anything.</summary>
    public static bool Carries(ColumnType declared, Type actual)
        => declared == ColumnType.Unknown
           || FromClr(actual) == declared
           || declared is ColumnType.Date or ColumnType.Timestamp && FromClr(actual) == ColumnType.Text;
}
