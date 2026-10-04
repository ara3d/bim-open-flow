using Ara3D.DataTable;

namespace BimOpenFlow.Nodes.Support;

/// <summary>Row selection over a flowing table without SQL.</summary>
public static class TableRows
{
    /// <summary>A copy of the table holding only the given rows, in the given order.</summary>
    public static IDataTable KeepRows(this IDataTable table, IReadOnlyList<int> rows, string name)
    {
        var builder = new DataTableBuilder(name);
        foreach (var c in table.Columns)
        {
            var values = new object?[rows.Count];
            for (var i = 0; i < rows.Count; i++)
                values[i] = c[rows[i]];
            builder.AddColumn(values, c.Descriptor.Name, c.Descriptor.Type);
        }
        return builder.Build();
    }
}
