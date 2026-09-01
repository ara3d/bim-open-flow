using Ara3D.DataTable;

namespace BimOpenFlow.Nodes.Cleaning;

/// <summary>Cleaning-specific table helpers; the shared column/ordinal
/// machinery lives in BimOpenFlow.Nodes.Support.TableColumns.</summary>
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
        => TableColumns.FreeName("__ord", table);
}
