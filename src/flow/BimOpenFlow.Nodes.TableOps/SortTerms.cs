using Ara3D.DataTable;
using SortKey = BimOpenFlow.Relations.SortKey;

namespace BimOpenFlow.Nodes.TableOps;

/// <summary>Parses table.sort syntax ("col desc, other") into sort keys, and into quoted SQL
/// order terms over a table's columns.</summary>
internal static class SortSyntax
{
    public static IReadOnlyList<string> SortTerms(this IDataTable table, string by, string kind)
        => ParseKeys(by, kind).Select(key => table.OrderTerm(key, kind)).ToList();

    /// <summary>The quoted SQL order term for a key, with the column spelled as the table stores it.</summary>
    public static string OrderTerm(this IDataTable table, SortKey key, string kind)
        => $"{table.CanonicalName(key.Column, kind).Ident()} {(key.Descending ? "DESC" : "ASC")}";

    public static IReadOnlyList<SortKey> ParseKeys(string by, string kind)
        => by.SplitNames().Select(entry => ParseKey(entry, kind)).ToList();

    private static SortKey ParseKey(string entry, string kind)
    {
        var tokens = entry.Split(' ', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        var descending = tokens.Length switch
        {
            1 => false,
            2 when tokens[1].Equals("desc", StringComparison.OrdinalIgnoreCase) => true,
            2 when tokens[1].Equals("asc", StringComparison.OrdinalIgnoreCase) => false,
            _ => throw new ArgumentException($"{kind}: cannot parse sort term '{entry}'."),
        };
        return new SortKey(tokens[0], descending);
    }
}
