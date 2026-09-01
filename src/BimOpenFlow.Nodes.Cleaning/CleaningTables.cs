using Ara3D.BimOpenSchema.DuckDb;
using Ara3D.DataTable;

namespace BimOpenFlow.Nodes.Cleaning;

/// <summary>Table helpers shared by the cleaning nodes: column lookup, row-ordinal
/// injection so generated SQL never trusts scan order, and error-prefixed SQL runs.</summary>
internal static class CleaningTables
{
    public static IDataColumn RequireColumn(this IDataTable table, string name, string kind)
        => table.Columns.FirstOrDefault(c =>
               string.Equals(c.Descriptor.Name, name, StringComparison.OrdinalIgnoreCase))
           ?? throw new ArgumentException($"{kind}: no column named '{name}'.");

    public static IDataColumn RequireTextColumn(this IDataTable table, string name, string kind)
    {
        var column = table.RequireColumn(name, kind);
        return column.IsText()
            ? column
            : throw new ArgumentException($"{kind}: column '{column.Descriptor.Name}' is not a text column.");
    }

    public static bool IsText(this IDataColumn column)
        => column.Descriptor.Type == typeof(string);

    /// <summary>A column name not already present, for the injected row ordinal.</summary>
    public static string OrdinalName(this IDataTable table)
    {
        var name = "__ord";
        while (table.Columns.Any(c =>
                   string.Equals(c.Descriptor.Name, name, StringComparison.OrdinalIgnoreCase)))
            name += "_";
        return name;
    }

    /// <summary>Copies the table with an appended 0-based row-ordinal column, so
    /// generated SQL can ORDER BY it instead of trusting scan order.</summary>
    public static IDataTable WithOrdinal(this IDataTable table, string ordinal)
    {
        var builder = new DataTableBuilder(table.Name);
        foreach (var c in table.Columns)
        {
            var cells = new object?[table.Rows.Count];
            for (var i = 0; i < cells.Length; i++)
                cells[i] = c[i];
            builder.AddColumn(cells, c.Descriptor.Name, c.Descriptor.Type);
        }
        var ordinals = new object?[table.Rows.Count];
        for (var i = 0; i < ordinals.Length; i++)
            ordinals[i] = (long)i;
        builder.AddColumn(ordinals, ordinal, typeof(long));
        return builder.Build();
    }

    /// <summary>Runs one statement over the table, rethrowing engine failures
    /// (bad regex, failed cast) with the node kind prefix.</summary>
    public static IDataTable RunSql(this IDataTable table, string sql, string kind)
    {
        try
        {
            return DuckTableSql.Run(table, sql);
        }
        catch (Exception e) when (e is not ArgumentException)
        {
            throw new ArgumentException($"{kind}: {e.Message}");
        }
    }
}
