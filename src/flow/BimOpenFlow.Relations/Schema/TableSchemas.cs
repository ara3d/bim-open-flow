using Ara3D.DataTable;

namespace BimOpenFlow.Relations;

/// <summary>The schema a built table actually has: the recorded columns a result is compared with.
/// A CLR type with no ColumnType stays Unknown, never guessed.</summary>
public static class TableSchemas
{
    public static Schema Of(IDataTable table)
        => new(table.Columns.Select(c => new Column(c.Descriptor.Name, TypeOf(c.Descriptor.Type))).ToList());

    public static ColumnType TypeOf(Type clrType)
    {
        var t = Nullable.GetUnderlyingType(clrType) ?? clrType;
        return t == typeof(bool) ? ColumnType.Boolean
            : t == typeof(sbyte) || t == typeof(byte) || t == typeof(short) || t == typeof(ushort)
              || t == typeof(int) || t == typeof(uint) || t == typeof(long) || t == typeof(ulong) ? ColumnType.Integer
            : t == typeof(float) || t == typeof(double) || t == typeof(decimal) ? ColumnType.Number
            : t == typeof(string) || t == typeof(char) ? ColumnType.Text
            : t == typeof(DateOnly) ? ColumnType.Date
            : t == typeof(DateTime) || t == typeof(DateTimeOffset) ? ColumnType.Timestamp
            : t == typeof(byte[]) ? ColumnType.Binary
            : ColumnType.Unknown;
    }
}
