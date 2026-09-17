using Ara3D.DataFlowEngine.Abstractions;
using Ara3D.DataFlowEngine.Expressions;
using Ara3D.DataTable;

namespace BimOpenFlow.Nodes.Compliance;

/// <summary>Shared table plumbing for the check nodes.</summary>
internal static class NodeTables
{
    public static IDataTable TableAt(this IReadOnlyList<FlowValue> inputs, int index)
        => inputs.Count > index && inputs[index] is TableValue t
            ? t.Table
            : throw new ArgumentException($"Input {index} must be a Table");

    /// <summary>Column name to index; on duplicate names the first column wins.</summary>
    public static IReadOnlyDictionary<string, int> ColumnIndexMap(this IDataTable table)
    {
        var map = new Dictionary<string, int>();
        for (var i = 0; i < table.Columns.Count; i++)
            map.TryAdd(table.Columns[i].Descriptor.Name, i);
        return map;
    }

    /// <summary>The expression environment: every column with a scalar-mappable CLR type.</summary>
    public static IReadOnlyDictionary<string, ScalarType> ScalarEnvironment(this IDataTable table)
    {
        var env = new Dictionary<string, ScalarType>();
        foreach (var column in table.Columns)
            if (Cells.ToScalarType(column.Descriptor.Type) is { } type)
                env.TryAdd(column.Descriptor.Name, type);
        return env;
    }

    /// <summary>
    /// Parses and type-checks a Boolean expression over the table's columns.
    /// Throws ArgumentException on parse/type errors or a non-Boolean result type.
    /// </summary>
    public static CheckedExpression CompileBoolean(this IDataTable table, string text, string paramName)
    {
        var expr = Expression.Parse(text).Check(table.ScalarEnvironment());
        if (!expr.Success)
            throw new ArgumentException($"Invalid '{paramName}' expression: {string.Join("; ", expr.Errors)}");
        if (expr.Type is { } t && t != ScalarType.Boolean)
            throw new ArgumentException($"'{paramName}' must be a Boolean expression, not {t}");
        return expr;
    }

    public static Func<string, Scalar?> RowLookup(this IDataTable table, IReadOnlyDictionary<string, int> columns, int row)
        => name => columns.TryGetValue(name, out var c) ? Cells.ToScalar(table.Cell(c, row)) : null;

    public static object? Cell(this IDataTable table, int column, int row)
        => table[column, row];

    /// <summary>The cell as non-null text; throws when null (metadata columns must be populated).</summary>
    public static string TextCell(this IDataTable table, int column, int row)
        => table.Cell(column, row) as string
           ?? throw new ArgumentException(
               $"Table '{table.Name}' column '{table.Columns[column].Descriptor.Name}' row {row} must be non-null Text");

    /// <summary>
    /// The input columns (order preserved) plus the four verdict-convention columns.
    /// Throws when the input already contains a reserved column name.
    /// </summary>
    public static IDataTable WithVerdicts(
        this IDataTable table, Verdict[] verdicts, string checkId, string title, string citation)
    {
        foreach (var reserved in VerdictSchema.Columns)
            if (table.FindColumn(reserved) != null)
                throw new ArgumentException(
                    $"Table '{table.Name}' already has a '{reserved}' column; check nodes take raw rows, not verdict tables");

        var count = table.Rows.Count;
        var columns = new List<MemoryColumn>(table.Columns.Count + 4);
        for (var i = 0; i < table.Columns.Count; i++)
            columns.Add(CopyColumn(table, i, columns.Count, count));
        columns.Add(TextColumn(VerdictSchema.VerdictColumn, columns.Count, count, r => verdicts[r].ToText()));
        columns.Add(TextColumn(VerdictSchema.CheckIdColumn, columns.Count, count, _ => checkId));
        columns.Add(TextColumn(VerdictSchema.CheckTitleColumn, columns.Count, count, _ => title));
        columns.Add(TextColumn(VerdictSchema.CitationColumn, columns.Count, count, _ => citation));
        return new MemoryTable(checkId, columns);
    }

    public static MemoryColumn CopyColumn(IDataTable table, int source, int index, int count)
    {
        var cells = new object?[count];
        for (var r = 0; r < count; r++)
            cells[r] = table.Cell(source, r);
        var descriptor = table.Columns[source].Descriptor;
        return new MemoryColumn(descriptor.Name, descriptor.Type, cells, index);
    }

    public static MemoryColumn TextColumn(string name, int index, int count, Func<int, string> cell)
    {
        var cells = new object?[count];
        for (var r = 0; r < count; r++)
            cells[r] = cell(r);
        return new MemoryColumn(name, typeof(string), cells, index);
    }
}
