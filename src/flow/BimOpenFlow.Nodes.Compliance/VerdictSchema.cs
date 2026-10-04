using Ara3D.DataTable;

namespace BimOpenFlow.Nodes.Compliance;

/// <summary>
/// The verdict-table column convention. A verdict table is any IDataTable
/// containing at least these four Text columns, matched by exact name.
/// See README.md for the full contract.
/// </summary>
public static class VerdictSchema
{
    public const string VerdictColumn = "verdict";
    public const string CheckIdColumn = "checkId";
    public const string CheckTitleColumn = "checkTitle";
    public const string CitationColumn = "citation";

    public static readonly IReadOnlyList<string> Columns =
        new[] { VerdictColumn, CheckIdColumn, CheckTitleColumn, CitationColumn };

    /// <summary>Index of the first column with this exact name, or null.</summary>
    public static int? FindColumn(this IDataTable table, string name)
    {
        for (var i = 0; i < table.Columns.Count; i++)
            if (table.Columns[i].Descriptor.Name == name)
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
