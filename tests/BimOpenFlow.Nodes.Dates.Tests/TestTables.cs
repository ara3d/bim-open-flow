using Ara3D.DataFlowEngine.Abstractions;
using Ara3D.DataTable;

namespace BimOpenFlow.Nodes.Dates.Tests;

internal static class TestTables
{
    public static TableValue TextColumn(string name, params string?[] values)
    {
        var builder = new DataTableBuilder("t");
        builder.AddColumn(values.Cast<object?>().ToArray(), name, typeof(string));
        return new TableValue(builder.Build());
    }

    public static TableValue TwoTextColumns(
        (string Name, string?[] Values) a, (string Name, string?[] Values) b)
    {
        var builder = new DataTableBuilder("t");
        builder.AddColumn(a.Values.Cast<object?>().ToArray(), a.Name, typeof(string));
        builder.AddColumn(b.Values.Cast<object?>().ToArray(), b.Name, typeof(string));
        return new TableValue(builder.Build());
    }
}
