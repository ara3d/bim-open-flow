using Ara3D.DataTable;

namespace BimOpenFlow.Nodes.Effects;

// TODO: third copy of this minimal table (Runs.RecordTable, Compliance.MemoryTable); hoist into a shared table utility.

internal sealed record MemoryDescriptor(string Name, Type Type) : IDataDescriptor;

/// <summary>An immutable column over an in-memory cell array; null cells are allowed.</summary>
internal sealed class MemoryColumn : IDataColumn
{
    private readonly object?[] _cells;

    public int ColumnIndex { get; }
    public IDataDescriptor Descriptor { get; }
    public int Count => _cells.Length;
    public object this[int n] => _cells[n]!;

    public MemoryColumn(string name, Type type, object?[] cells, int index)
    {
        Descriptor = new MemoryDescriptor(name, type);
        _cells = cells;
        ColumnIndex = index;
    }
}

internal sealed class MemoryRow : IDataRow
{
    public int RowIndex { get; }
    public IDataTable DataTable { get; }
    public object this[int index] => DataTable[index, RowIndex];

    public IReadOnlyList<object> Values
    {
        get
        {
            var values = new object[DataTable.Columns.Count];
            for (var i = 0; i < values.Length; i++)
                values[i] = DataTable[i, RowIndex];
            return values;
        }
    }

    public MemoryRow(IDataTable table, int rowIndex)
    {
        DataTable = table;
        RowIndex = rowIndex;
    }
}

/// <summary>Minimal immutable in-memory IDataTable used to build node outputs.</summary>
internal sealed class MemoryTable : IDataTable
{
    public string Name { get; }
    public IReadOnlyList<IDataColumn> Columns { get; }
    public IReadOnlyList<IDataRow> Rows { get; }
    public object this[int column, int row] => Columns[column][row];

    public MemoryTable(string name, IReadOnlyList<MemoryColumn> columns)
    {
        Name = name;
        Columns = columns;
        var count = columns.Count == 0 ? 0 : columns[0].Count;
        foreach (var c in columns)
            if (c.Count != count)
                throw new ArgumentException($"Column '{c.Descriptor.Name}' has {c.Count} cells, expected {count}");
        var rows = new IDataRow[count];
        for (var i = 0; i < count; i++)
            rows[i] = new MemoryRow(this, i);
        Rows = rows;
    }
}
