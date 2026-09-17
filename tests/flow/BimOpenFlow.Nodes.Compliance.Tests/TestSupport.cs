using Ara3D.DataFlowEngine.Abstractions;
using Ara3D.DataTable;
using BimOpenFlow.Nodes.Compliance;

namespace BimOpenFlow.Nodes.Compliance.Tests;

internal sealed class StubContext : IEvalContext
{
    public bool IsRun => false;
    public CancellationToken Cancellation => CancellationToken.None;
    public List<string> Warnings { get; } = new();
    public void Warn(string message) => Warnings.Add(message);
}

internal static class TestSupport
{
    public static IDataTable Table(string name, params (string Name, Type Type, object?[] Cells)[] columns)
        => new MemoryTable(name,
            columns.Select((c, i) => new MemoryColumn(c.Name, c.Type, c.Cells, i)).ToList());

    /// <summary>A minimal verdict table: one metadata triple, one verdict text per row.</summary>
    public static IDataTable VerdictTable(string checkId, string title, string citation, params Verdict[] verdicts)
        => Table(checkId,
            ("verdict", typeof(string), verdicts.Select(v => (object?)v.ToText()).ToArray()),
            ("checkId", typeof(string), Repeat(checkId, verdicts.Length)),
            ("checkTitle", typeof(string), Repeat(title, verdicts.Length)),
            ("citation", typeof(string), Repeat(citation, verdicts.Length)));

    public static object?[] Repeat(string value, int count)
        => Enumerable.Repeat((object?)value, count).ToArray();

    public static ParamValues Params(params (string Name, string Value)[] values)
        => new(values.ToDictionary(v => v.Name, v => v.Value));

    public static IDataTable EvalTable(this IFlowNode node, ParamValues parameters, params IDataTable[] tables)
        => ((TableValue)node.Eval(new StubContext(),
            tables.Select(t => (FlowValue)new TableValue(t)).ToList(), parameters)[0]).Table;

    public static string[] ColumnNames(this IDataTable table)
        => table.Columns.Select(c => c.Descriptor.Name).ToArray();

    public static string[] VerdictTexts(this IDataTable table)
    {
        var column = table.RequireColumn("verdict");
        return Enumerable.Range(0, table.Rows.Count).Select(r => (string)table[column, r]).ToArray();
    }

    public static object? Cell(this IDataTable table, string column, int row)
        => table[table.RequireColumn(column), row];
}
