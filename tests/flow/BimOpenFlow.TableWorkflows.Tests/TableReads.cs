using Ara3D.DataFlowEngine;
using Ara3D.DataFlowEngine.Abstractions;
using Ara3D.DataFlowEngine.TestKit;
using Ara3D.DataTable;

namespace BimOpenFlow.TableWorkflows.Tests;

/// <summary>Reads table outputs and cells out of an evaluated session.</summary>
public static class TableReads
{
    public static FlowTestSession NewTableSession()
        => new(BimOpenFlow.Host.HostComposition.TablePacks());

    public static IDataTable Table(this FlowTestSession session, string nodeId)
    {
        var result = session.Result(nodeId);
        if (result.Status == NodeStatus.Unavailable && result.BlockingNodeId is { } blocking)
            Assert.Fail($"Node '{nodeId}' is Unavailable; blocked by '{blocking}' "
                + $"({session.Result(blocking).Status}: {session.Result(blocking).Error})");
        return ((TableValue)session.Output(nodeId, "table")).Table;
    }

    public static object? Cell(this IDataTable table, string column, int row)
        => table[table.Columns.Single(c => c.Descriptor.Name == column).ColumnIndex, row];

    public static List<object?> Column(this IDataTable table, string column)
        => Enumerable.Range(0, table.Rows.Count).Select(r => table.Cell(column, r)).ToList();
}
