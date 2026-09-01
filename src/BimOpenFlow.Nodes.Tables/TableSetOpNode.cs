using Ara3D.DataFlowEngine.Abstractions;
using Ara3D.DataTable;

namespace BimOpenFlow.Nodes.Tables;

/// <summary>Row-set algebra on a key column: a's columns and row order pass
/// through; union appends b rows whose key is absent from a.</summary>
public sealed class TableSetOpNode : IFlowNode
{
    public const string Kind = "table.setOp";

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
            new ParamSpec("op", ParamKind.Enum, "intersect", ["union", "intersect", "subtract"]),
            new ParamSpec("key", ParamKind.Text),
        ],
        "Keeps a's rows by key-set operation with b: union, intersect, or subtract.");

    public IReadOnlyList<FlowValue> Eval(IEvalContext context, IReadOnlyList<FlowValue> inputs, ParamValues parameters)
    {
        var a = inputs.TableInput(0, Kind);
        var b = inputs.TableInput(1, Kind);
        var op = parameters.RequiredEnum("op", Kind, "intersect", "union", "intersect", "subtract");
        var key = parameters.RequiredText("key", Kind);
        var aKeyCol = a.RequireColumn(key, Kind);
        var bKeyCol = b.RequireColumn(key, Kind);

        if (op == "union")
            return [new TableValue(Union(a, b, aKeyCol, bKeyCol))];

        var bKeys = b.KeySet(bKeyCol);
        var kept = new List<int>();
        for (var row = 0; row < a.RowCount(); row++)
        {
            var inB = TableOps.CanonicalText(a[aKeyCol, row]) is { } k && bKeys.Contains(k);
            if (inB == (op == "intersect"))
                kept.Add(row);
        }
        return [new TableValue(a.SelectRows(kept, a.Name))];
    }

    /// <summary>All of a, then b rows whose key is absent from a; b must share a's column set.</summary>
    private static IDataTable Union(IDataTable a, IDataTable b, int aKeyCol, int bKeyCol)
    {
        var aNames = a.Columns.Select(c => c.Descriptor.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var bNames = b.Columns.Select(c => c.Descriptor.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var missing = aNames.Except(bNames).ToList();
        var extra = bNames.Except(aNames).ToList();
        if (missing.Count > 0 || extra.Count > 0)
            throw new ArgumentException(
                $"{Kind}: union requires b to share a's columns; " +
                $"missing from b: [{string.Join(", ", missing)}], extra in b: [{string.Join(", ", extra)}].");

        var aKeys = a.KeySet(aKeyCol);
        var appended = new List<int>();
        for (var row = 0; row < b.RowCount(); row++)
            if (!(TableOps.CanonicalText(b[bKeyCol, row]) is { } key && aKeys.Contains(key)))
                appended.Add(row);

        var builder = new DataTableBuilder(a.Name);
        foreach (var c in a.Columns)
        {
            var bCol = b.ColumnIndex(c.Descriptor.Name);
            var values = new object?[a.RowCount() + appended.Count];
            for (var row = 0; row < a.RowCount(); row++)
                values[row] = a[c.ColumnIndex, row];
            for (var i = 0; i < appended.Count; i++)
                values[a.RowCount() + i] = b[bCol, appended[i]];
            builder.AddColumn(values, c.Descriptor.Name, c.Descriptor.Type);
        }
        return builder.Build();
    }
}
