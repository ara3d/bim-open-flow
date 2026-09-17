using Ara3D.DataTable;

namespace BimOpenFlow.Nodes.TableOps;

/// <summary>Parses table.sort syntax ("col desc, other") into quoted SQL
/// order terms over the table's columns.</summary>
internal static class SortSyntax
{
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
