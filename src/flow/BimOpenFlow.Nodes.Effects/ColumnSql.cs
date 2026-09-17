using System.Globalization;
using Ara3D.DataTable;

namespace BimOpenFlow.Nodes.Effects;

/// <summary>The three storage kinds the database sinks write: booleans and integers
/// as integers, floating point as doubles, everything else as text.</summary>
internal enum ColumnKind
{
    Integer,
    Number,
    Text,
}

/// <summary>Column classification, cell normalization, and identifier quoting
/// shared by the SQLite and DuckDB sinks.</summary>
internal static class ColumnSql
{
    public static ColumnKind Classify(Type type)
        => Type.GetTypeCode(type) switch
        {
            TypeCode.Boolean or TypeCode.SByte or TypeCode.Byte
                or TypeCode.Int16 or TypeCode.UInt16 or TypeCode.Int32 or TypeCode.UInt32
                or TypeCode.Int64 or TypeCode.UInt64 => ColumnKind.Integer,
            TypeCode.Single or TypeCode.Double or TypeCode.Decimal => ColumnKind.Number,
            _ => ColumnKind.Text,
        };

    public static ColumnKind[] Kinds(IDataTable table)
    {
        var kinds = new ColumnKind[table.Columns.Count];
        for (var i = 0; i < kinds.Length; i++)
            kinds[i] = Classify(table.Columns[i].Descriptor.Type);
        return kinds;
    }

    /// <summary>Integer cells become long (booleans 1/0), Number cells double,
    /// Text cells invariant text; null stays null.</summary>
    public static object? Normalize(object? cell, ColumnKind kind)
        => cell == null
            ? null
            : kind switch
            {
                ColumnKind.Integer => cell is bool b ? (b ? 1L : 0L) : Convert.ToInt64(cell, CultureInfo.InvariantCulture),
                ColumnKind.Number => Convert.ToDouble(cell, CultureInfo.InvariantCulture),
                _ => CsvWriting.FormatCell(cell),
            };

    public static string QuoteIdent(string name)
        => "\"" + name.Replace("\"", "\"\"") + "\"";

    public static string[] ColumnNames(IDataTable table)
    {
        var names = new string[table.Columns.Count];
        for (var i = 0; i < names.Length; i++)
            names[i] = table.Columns[i].Descriptor.Name;
        return names;
    }

    /// <summary>Append compatibility: the existing table must carry exactly the
    /// input's column names (case-insensitive, order-free).</summary>
    public static void RequireCompatibleColumns(string kind, string table, IReadOnlyList<string> existing, IReadOnlyList<string> incoming)
    {
        var existingSet = new HashSet<string>(existing, StringComparer.OrdinalIgnoreCase);
        var incomingSet = new HashSet<string>(incoming, StringComparer.OrdinalIgnoreCase);
        if (!existingSet.SetEquals(incomingSet))
            throw new ArgumentException(
                $"{kind}: table '{table}' has columns [{string.Join(", ", existing)}], "
                + $"incompatible with input columns [{string.Join(", ", incoming)}]");
    }
}
