using Ara3D.DataFlowEngine.Abstractions;
using Ara3D.DataFlowEngine.TestKit;
using Ara3D.DataTable;

namespace BimOpenFlow.Nodes.Tables.Tests;

/// <summary>Tables-specific test sugar; the shared helpers live in
/// Ara3D.DataFlowEngine.TestKit.NodeTestHelpers.</summary>
internal static class TablesTestHelpers
{
    public static IDataTable EvalTable(this IFlowNode node, IReadOnlyList<IDataTable> tables,
        params (string Name, string Value)[] ps)
        => node.EvalTable(new FakeEvalContext(), tables, ps);

    public static IDataTable EvalTable(this IFlowNode node, IEvalContext context, IReadOnlyList<IDataTable> tables,
        params (string Name, string Value)[] ps)
        => ((TableValue)node.Eval(context, tables.Select(FlowValue (t) => new TableValue(t)).ToList(),
            NodeTestHelpers.Params(ps))[0]).Table;

    /// <summary>Orders: Id, CustomerId (row 2 has a null CustomerId, row 3 an unknown one).</summary>
    public static IDataTable Orders()
    {
        var builder = new DataTableBuilder("orders");
        builder.AddColumn(new object?[] { 1L, 2L, 3L, 4L }, "Id", typeof(long));
        builder.AddColumn(new object?[] { "C1", "C2", null, "C9" }, "CustomerId", typeof(string));
        return builder.Build();
    }

    /// <summary>Customers: CustomerId, Name.</summary>
    public static IDataTable Customers()
    {
        var builder = new DataTableBuilder("customers");
        builder.AddColumn(new object?[] { "C1", "C2", "C3" }, "CustomerId", typeof(string));
        builder.AddColumn(new object?[] { "Alice", "Bob", "Carol" }, "Name", typeof(string));
        return builder.Build();
    }
}
