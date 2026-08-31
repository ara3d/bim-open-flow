using System.Collections.Generic;
using Ara3D.DataTable;
using BimOpenFlow.Contracts;
using BimOpenFlow.Publishing;

namespace BimOpenFlow.Reports;

/// <summary>Counts by verdict for one verdict table; Worst follows the
/// rollup severity order Fail &gt; NeedsReview &gt; InfoNotAvailable &gt; Pass.</summary>
public sealed record VerdictCounts(int Pass, int Fail, int NeedsReview, int InfoNotAvailable)
{
    public int Total => Pass + Fail + NeedsReview + InfoNotAvailable;

    public Verdict Worst
        => Fail > 0 ? Verdict.Fail
            : NeedsReview > 0 ? Verdict.NeedsReview
            : InfoNotAvailable > 0 ? Verdict.InfoNotAvailable
            : Verdict.Pass;
}

/// <summary>
/// Detects tables following the Nodes.Compliance verdict-table convention:
/// Text columns named exactly verdict, checkId, checkTitle, citation, with
/// every verdict cell one of the four Verdict member names.
/// </summary>
public static class VerdictTables
{
    public const string VerdictColumn = "verdict";
    public const string CheckIdColumn = "checkId";
    public const string CheckTitleColumn = "checkTitle";
    public const string CitationColumn = "citation";

    public static bool IsVerdictTable(IDataTable table)
        => TryCount(table, out _);

    public static VerdictCounts Count(IDataTable table)
        => TryCount(table, out var counts)
            ? counts!
            : throw new System.ArgumentException($"Table '{table.Name}' is not a verdict table", nameof(table));

    public static bool TryCount(IDataTable table, out VerdictCounts? counts)
    {
        counts = null;
        var verdicts = FindTextColumn(table, VerdictColumn);
        if (verdicts is null
            || FindTextColumn(table, CheckIdColumn) is null
            || FindTextColumn(table, CheckTitleColumn) is null
            || FindTextColumn(table, CitationColumn) is null)
            return false;

        int pass = 0, fail = 0, review = 0, info = 0;
        for (var r = 0; r < verdicts.Count; r++)
            switch (verdicts[r] as string)
            {
                case nameof(Verdict.Pass): pass++; break;
                case nameof(Verdict.Fail): fail++; break;
                case nameof(Verdict.NeedsReview): review++; break;
                case nameof(Verdict.InfoNotAvailable): info++; break;
                default: return false;
            }
        counts = new(pass, fail, review, info);
        return true;
    }

    private static IDataColumn? FindTextColumn(IDataTable table, string name)
    {
        foreach (var c in table.Columns)
            if (c.Descriptor.Name == name)
                return TableJson.TryToColumnType(c.Descriptor.Type, out var t) && t == ColumnType.Text
                    ? c
                    : null;
        return null;
    }
}
