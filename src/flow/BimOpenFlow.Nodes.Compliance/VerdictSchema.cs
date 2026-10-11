using Ara3D.BimOpenSchema;
using Ara3D.DataTable;

namespace BimOpenFlow.Nodes.Compliance;

/// <summary>
/// The verdict-table column convention. A verdict table is any IDataTable
/// containing at least the four Text columns in <see cref="Columns"/>, matched by
/// name ignoring case (older recorded tables use camelCase).
/// See README.md for the full contract.
/// </summary>
public static class VerdictSchema
{
    public static readonly IReadOnlyList<string> Columns =
        new[] { IssueColumns.Verdict, IssueColumns.CheckId, IssueColumns.CheckTitle, IssueColumns.Citation };

    /// <summary>Index of the first column with this name, ignoring case, or null.</summary>
    public static int? FindColumn(this IDataTable table, string name)
    {
        for (var i = 0; i < table.Columns.Count; i++)
            if (string.Equals(table.Columns[i].Descriptor.Name, name, StringComparison.OrdinalIgnoreCase))
                return i;
        return null;
    }

    public static int RequireColumn(this IDataTable table, string name)
        => table.FindColumn(name)
           ?? throw new ArgumentException($"Table '{table.Name}' has no '{name}' column");

    public static bool IsVerdictTable(this IDataTable table)
        => Columns.All(c => table.FindColumn(c) != null);

    public static void RequireVerdictTable(this IDataTable table)
    {
        foreach (var c in Columns)
            table.RequireColumn(c);
    }
}
