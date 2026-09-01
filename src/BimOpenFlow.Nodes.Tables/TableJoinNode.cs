using Ara3D.DataFlowEngine.Abstractions;
using Ara3D.DataTable;

namespace BimOpenFlow.Nodes.Tables;

/// <summary>Joins table b's columns onto table a by key column. Unmatched and
/// duplicate-key counts surface as warnings, never silently.</summary>
public sealed class TableJoinNode : IFlowNode
{
    public const string Kind = "table.join";

    public NodeSpec Spec { get; } = new(
        Kind, 1, NodeCapability.Pure,
        Inputs:
        [
            new PortSpec("a", PortType.Table),
            new PortSpec("b", PortType.Table),
        ],
        Outputs: [new PortSpec("table", PortType.Table)],
        Params:
        [
            new ParamSpec("aKey", ParamKind.Text),
            new ParamSpec("bKey", ParamKind.Text, ""),
            new ParamSpec("mode", ParamKind.Enum, "left", ["left", "inner", "full", "semi", "anti"]),
        ],
        "Joins b's columns onto a by key (bKey defaults to aKey). left keeps all a rows; inner keeps matches; full also appends unmatched b rows; semi/anti keep only a rows with/without a match and attach no b columns.");

    public IReadOnlyList<FlowValue> Eval(IEvalContext context, IReadOnlyList<FlowValue> inputs, ParamValues parameters)
    {
        var a = inputs.TableInput(0, Kind);
        var b = inputs.TableInput(1, Kind);
        var aKey = parameters.RequiredText("aKey", Kind);
        var bKeyText = parameters.GetText("bKey");
        var bKey = string.IsNullOrWhiteSpace(bKeyText) ? aKey : bKeyText;
        var mode = parameters.RequiredEnum("mode", Kind, "left", "left", "inner", "full", "semi", "anti");
        var aKeyCol = a.RequireColumn(aKey, Kind);
        var bKeyCol = b.RequireColumn(bKey, Kind);

        var lookup = BuildLookup(context, b, bKeyCol);
        if (mode is "semi" or "anti")
            return [new TableValue(SemiAnti(context, a, aKeyCol, lookup, keepMatched: mode == "semi"))];

        var (aRows, bRows, unmatched) = MatchRows(a, aKeyCol, lookup, keepUnmatched: mode != "inner");
        if (unmatched > 0)
            context.Warn($"{Kind}: {unmatched} of {a.RowCount()} rows unmatched");
        if (mode == "full")
            AppendUnmatchedB(b, bKeyCol, lookup, aRows, bRows);

        var builder = new DataTableBuilder(a.Name);
        foreach (var c in a.Columns)
        {
            var values = Select(a, c.ColumnIndex, aRows);
            if (c.ColumnIndex == aKeyCol)
                for (var i = 0; i < values.Length; i++)
                    if (aRows[i] < 0)
                        values[i] = b[bKeyCol, bRows[i]];
            builder.AddColumn(values, c.Descriptor.Name, c.Descriptor.Type);
        }
        var aNames = a.Columns.Select(c => c.Descriptor.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var c in b.Columns)
        {
            if (c.ColumnIndex == bKeyCol)
                continue;
            var name = aNames.Contains(c.Descriptor.Name) ? $"{c.Descriptor.Name}_b" : c.Descriptor.Name;
            builder.AddColumn(Select(b, c.ColumnIndex, bRows), name, c.Descriptor.Type);
        }
        return [new TableValue(builder.Build())];
    }

    /// <summary>Only a's columns: rows with a match (semi) or without one (anti).</summary>
    private static IDataTable SemiAnti(IEvalContext context, IDataTable a, int keyCol,
        Dictionary<string, int> lookup, bool keepMatched)
    {
        var rows = new List<int>();
        var unmatched = 0;
        for (var row = 0; row < a.RowCount(); row++)
        {
            var matched = TableOps.CanonicalText(a[keyCol, row]) is { } key && lookup.ContainsKey(key);
            if (!matched)
                unmatched++;
            if (matched == keepMatched)
                rows.Add(row);
        }
        if (unmatched > 0)
            context.Warn($"{Kind}: {unmatched} of {a.RowCount()} rows unmatched");
        return a.SelectRows(rows, a.Name);
    }

    /// <summary>Full mode: appends each b row whose key no a row matched (a side lands null).</summary>
    private static void AppendUnmatchedB(IDataTable b, int keyCol, Dictionary<string, int> lookup,
        List<int> aRows, List<int> bRows)
    {
        var matched = bRows.Where(r => r >= 0).ToHashSet();
        for (var row = 0; row < b.RowCount(); row++)
        {
            var key = TableOps.CanonicalText(b[keyCol, row]);
            if (key != null && lookup.TryGetValue(key, out var first) && matched.Contains(first))
                continue;
            aRows.Add(-1);
            bRows.Add(row);
        }
    }

    /// <summary>Key text to first b row with that key; warns when b has duplicates.</summary>
    private static Dictionary<string, int> BuildLookup(IEvalContext context, IDataTable b, int keyCol)
    {
        var lookup = new Dictionary<string, int>();
        var duplicates = false;
        for (var row = 0; row < b.RowCount(); row++)
            if (TableOps.CanonicalText(b[keyCol, row]) is { } key)
                duplicates |= !lookup.TryAdd(key, row);
        if (duplicates)
            context.Warn($"{Kind}: duplicate keys in b: first occurrence wins");
        return lookup;
    }

    /// <summary>Pairs each kept a row with its b row (-1 when unmatched in left mode).</summary>
    private static (List<int> ARows, List<int> BRows, int Unmatched) MatchRows(
        IDataTable a, int keyCol, Dictionary<string, int> lookup, bool keepUnmatched)
    {
        var aRows = new List<int>();
        var bRows = new List<int>();
        var unmatched = 0;
        for (var row = 0; row < a.RowCount(); row++)
        {
            if (TableOps.CanonicalText(a[keyCol, row]) is { } key && lookup.TryGetValue(key, out var bRow))
            {
                aRows.Add(row);
                bRows.Add(bRow);
            }
            else
            {
                unmatched++;
                if (keepUnmatched)
                {
                    aRows.Add(row);
                    bRows.Add(-1);
                }
            }
        }
        return (aRows, bRows, unmatched);
    }

    private static object?[] Select(IDataTable table, int column, IReadOnlyList<int> rows)
    {
        var values = new object?[rows.Count];
        for (var i = 0; i < rows.Count; i++)
            values[i] = rows[i] < 0 ? null : table[column, rows[i]];
        return values;
    }
}
