using Ara3D.DataFlowEngine.Abstractions;
using Ara3D.DataTable;

namespace BimOpenFlow.Nodes.Tables.Tests;

internal sealed class FakeEvalContext : IEvalContext
{
    public bool IsRun => false;
    public CancellationToken Cancellation => CancellationToken.None;
    public List<string> Warnings { get; } = [];
    public void Warn(string message) => Warnings.Add(message);
}

internal static class NodeTestHelpers
{
    public static ParamValues Params(params (string Name, string Value)[] ps)
        => new(ps.ToDictionary(p => p.Name, p => p.Value));

    public static IDataTable EvalTable(this IFlowNode node, IReadOnlyList<IDataTable> tables,
        params (string Name, string Value)[] ps)
        => node.EvalTable(new FakeEvalContext(), tables, ps);

    public static IDataTable EvalTable(this IFlowNode node, IEvalContext context, IReadOnlyList<IDataTable> tables,
        params (string Name, string Value)[] ps)
        => ((TableValue)node.Eval(context, tables.Select(FlowValue (t) => new TableValue(t)).ToList(), Params(ps))[0]).Table;

    public static object? Cell(this IDataTable table, string column, int row)
        => table[table.Columns.Single(c => c.Descriptor.Name == column).ColumnIndex, row];

    public static IReadOnlyList<string> ColumnNames(this IDataTable table)
        => table.Columns.Select(c => c.Descriptor.Name).ToList();

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
