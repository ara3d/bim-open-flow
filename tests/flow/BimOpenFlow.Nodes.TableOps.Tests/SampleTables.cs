using Ara3D.DataFlowEngine.Abstractions;
using Ara3D.DataTable;

namespace BimOpenFlow.Nodes.TableOps.Tests;

/// <summary>A small in-code table with nulls, and single-table evaluation sugar; the
/// shared helpers live in Ara3D.DataFlowEngine.TestKit.NodeTestHelpers.</summary>
internal static class SampleTables
{
    public static IReadOnlyList<FlowValue> Eval(this IFlowNode node, IDataTable? input,
        params (string Name, string Value)[] ps)
        => node.Eval(NodeTestHelpers.Ctx, input == null ? [] : [new TableValue(input)],
            NodeTestHelpers.Params(ps));

    public static IDataTable EvalTable(this IFlowNode node, IDataTable? input,
        params (string Name, string Value)[] ps)
        => ((TableValue)node.Eval(input, ps)[0]).Table;

    /// <summary>Four rows with a null Name (row 3) and a null Height (row 1).</summary>
    public static IDataTable SampleTable()
    {
        var builder = new DataTableBuilder("sample");
        builder.AddColumn(new object?[] { "Wall-1", "Wall-2", "Door-1", null }, "Name", typeof(string));
        builder.AddColumn(new object?[] { 2.5, null, 2.1, 3.0 }, "Height", typeof(double));
        builder.AddColumn(new object?[] { 1L, 2L, 3L, 4L }, "Count", typeof(long));
        builder.AddColumn(new object?[] { "Walls", "Walls", "Doors", "Doors" }, "Category", typeof(string));
        return builder.Build();
    }
}
