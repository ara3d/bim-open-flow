using Ara3D.DataTable;
using BimOpenFlow.Nodes.Support;

namespace BimOpenFlow.GraphText;

/// <summary>The verdict digest of a table with `checkId` and `verdict` columns (check.rule,
/// check.required, check.union): one line per check with its count per verdict.</summary>
public static class VerdictText
{
    public static bool IsVerdictTable(IDataTable table)
        => TableText.HasColumns(table, "checkId", "verdict");

    public static IReadOnlyList<string> Lines(IDataTable table, DigestContext context)
    {
        if (!IsVerdictTable(table))
            return [];
        var check = table.ColumnIndex("checkId");
        var verdict = table.ColumnIndex("verdict");
        return Enumerable.Range(0, table.RowCount())
            .Select(row => (Check: TableColumns.CellText(table[check, row]) ?? "null", Verdict: TableColumns.CellText(table[verdict, row]) ?? "null"))
            .GroupBy(v => v.Check)
            .OrderBy(g => g.Key, StringComparer.Ordinal)
            .Select(g => $"verdicts {Literals.QuoteLine(g.Key)}: " + string.Join(", ", g
                .GroupBy(v => v.Verdict)
                .OrderBy(v => v.Key, StringComparer.Ordinal)
                .Select(v => $"{v.Key} {v.Count()}")))
            .Take(context.Options.MaxListed)
            .ToList();
    }
}
