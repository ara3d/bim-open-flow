using Ara3D.DataTable;

namespace BimOpenFlow.Publishing.Tests;

public sealed record TestDescriptor(string Name, Type Type) : IDataDescriptor;

public sealed class TestColumn : IDataColumn
{
    private readonly object?[] _cells;
    public int ColumnIndex { get; }
    public IDataDescriptor Descriptor { get; }
    public int Count => _cells.Length;
    public object this[int n] => _cells[n]!;

    public TestColumn(string name, Type type, object?[] cells, int index)
    {
        Descriptor = new TestDescriptor(name, type);
        _cells = cells;
        ColumnIndex = index;
    }
}

public sealed class TestRow : IDataRow
{
    public int RowIndex { get; }
    public IDataTable DataTable { get; }
    public object this[int index] => DataTable[index, RowIndex];

    public IReadOnlyList<object> Values
        => Enumerable.Range(0, DataTable.Columns.Count).Select(i => this[i]).ToList();

    public TestRow(IDataTable table, int rowIndex)
    {
        DataTable = table;
        RowIndex = rowIndex;
    }
}

/// <summary>Minimal immutable IDataTable for test fixtures.</summary>
public sealed class TestTable : IDataTable
{
    public string Name { get; }
    public IReadOnlyList<IDataColumn> Columns { get; }
    public IReadOnlyList<IDataRow> Rows { get; }
    public object this[int column, int row] => Columns[column][row];

    public TestTable(string name, params TestColumn[] columns)
    {
        Name = name;
        Columns = columns;
        var count = columns.Length == 0 ? 0 : columns[0].Count;
        Rows = Enumerable.Range(0, count).Select(i => (IDataRow)new TestRow(this, i)).ToList();
    }

    public static TestTable Sample()
        => new("sample",
            new TestColumn("name", typeof(string), new object?[] { "Wall <A>", "Door \"B\"", null }, 0),
            new TestColumn("count", typeof(long), new object?[] { 3L, 5L, 7L }, 1),
            new TestColumn("area", typeof(double), new object?[] { 1.5, null, 2.25 }, 2),
            new TestColumn("ok", typeof(bool), new object?[] { true, false, null }, 3));
}
