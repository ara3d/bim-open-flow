using System.Globalization;
using Ara3D.BimOpenSchema.DuckDb;
using Ara3D.DataTable;

namespace BimOpenFlow.Nodes.TableOps;

/// <summary>Column lookup, ordinal injection, and SQL fragments shared by the
/// TableOps nodes. Duplicated per pack because packs do not reference each other.</summary>
internal static class TableColumns
{
    public static IReadOnlyList<string> Names(this IDataTable table)
        => table.Columns.Select(c => c.Descriptor.Name).ToList();

    public static int ColumnIndex(this IDataTable table, string name)
    {
        for (var i = 0; i < table.Columns.Count; i++)
            if (string.Equals(table.Columns[i].Descriptor.Name, name, StringComparison.OrdinalIgnoreCase))
                return i;
        return -1;
    }

    public static int RequireColumn(this IDataTable table, string name, string kind)
        => table.ColumnIndex(name) is var i && i >= 0
            ? i
            : throw new ArgumentException($"{kind}: no column named '{name}'.");

    /// <summary>The stored spelling of a column name, resolving case-insensitively.</summary>
    public static string CanonicalName(this IDataTable table, string name, string kind)
        => table.Columns[table.RequireColumn(name, kind)].Descriptor.Name;

    public static int RowCount(this IDataTable table)
        => table.Columns.Count == 0 ? 0 : table.Columns[0].Count;

    public static string Ident(this string name)
        => DuckTableSql.QuoteIdent(name);

    public static string Literal(this string text)
        => DuckTableSql.QuoteLiteral(text);

    /// <summary>A column name not present in any of the given tables, derived
    /// from the seed by appending underscores.</summary>
    public static string FreeName(string seed, params IDataTable[] tables)
    {
        var name = seed;
        while (tables.Any(t => t.ColumnIndex(name) >= 0))
            name += "_";
        return name;
    }

    /// <summary>A copy of the table with an extra 0-based Integer ordinal column,
    /// so generated SQL can ORDER BY the table's actual row order.</summary>
    public static IDataTable WithOrdinal(this IDataTable table, string ordinal)
    {
        var rows = table.RowCount();
        var builder = new DataTableBuilder(table.Name);
        foreach (var c in table.Columns)
        {
            var cells = new object?[rows];
            for (var row = 0; row < rows; row++)
                cells[row] = table[c.ColumnIndex, row];
            builder.AddColumn(cells, c.Descriptor.Name, c.Descriptor.Type);
        }
        var ordinals = new object?[rows];
        for (var row = 0; row < rows; row++)
            ordinals[row] = (long)row;
        builder.AddColumn(ordinals, ordinal, typeof(long));
        return builder.Build();
    }

    /// <summary>The wire column-type name (Boolean/Integer/Number/Text) for a CLR column type.</summary>
    public static string KindName(Type type)
    {
        var t = Nullable.GetUnderlyingType(type) ?? type;
        if (t == typeof(bool)) return "Boolean";
        if (t == typeof(sbyte) || t == typeof(byte) || t == typeof(short) || t == typeof(ushort)
            || t == typeof(int) || t == typeof(uint) || t == typeof(long) || t == typeof(ulong))
            return "Integer";
        if (t == typeof(float) || t == typeof(double) || t == typeof(decimal)) return "Number";
        return "Text";
    }

    /// <summary>Invariant text of a cell; null for absent values.</summary>
    public static string? CellText(object? cell)
        => cell switch
        {
            null or DBNull => null,
            string s => s,
            bool b => b ? "true" : "false",
            IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
            _ => cell.ToString(),
        };

    /// <summary>Parses table.sort syntax ("col desc, other") into quoted SQL
    /// order terms over the table's columns.</summary>
    public static IReadOnlyList<string> SortTerms(this IDataTable table, string by, string kind)
        => by.SplitNames().Select(entry => table.SortTerm(entry, kind)).ToList();

    private static string SortTerm(this IDataTable table, string entry, string kind)
    {
        var tokens = entry.Split(' ', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        var direction = tokens.Length switch
        {
            1 => "ASC",
            2 when tokens[1].Equals("desc", StringComparison.OrdinalIgnoreCase) => "DESC",
            2 when tokens[1].Equals("asc", StringComparison.OrdinalIgnoreCase) => "ASC",
            _ => throw new ArgumentException($"{kind}: cannot parse sort term '{entry}'."),
        };
        return $"{table.CanonicalName(tokens[0], kind).Ident()} {direction}";
    }
}
