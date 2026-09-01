using Ara3D.DataFlowEngine.Abstractions;
using Ara3D.DataTable;

namespace BimOpenFlow.Nodes.TableOps.Tests;

internal sealed class FakeEvalContext : IEvalContext
{
    public bool IsRun => false;
    public CancellationToken Cancellation => CancellationToken.None;
    public List<string> Warnings { get; } = [];
    public void Warn(string message) => Warnings.Add(message);
}

internal static class NodeTestHelpers
{
    public static readonly IEvalContext Ctx = new FakeEvalContext();

    public static ParamValues Params(params (string Name, string Value)[] ps)
        => new(ps.ToDictionary(p => p.Name, p => p.Value));

    public static IDataTable EvalTable(this IFlowNode node, IReadOnlyList<FlowValue> inputs,
        params (string Name, string Value)[] ps)
        => ((TableValue)node.Eval(Ctx, inputs, Params(ps))[0]).Table;

    /// <summary>Builds an in-memory table named "t" from (name, type, cells) columns.</summary>
    public static TableValue Table(params (string Name, Type Type, object?[] Cells)[] columns)
    {
        var builder = new DataTableBuilder("t");
        foreach (var (name, type, cells) in columns)
            builder.AddColumn(cells, name, type);
        return new TableValue(builder.Build());
    }

    /// <summary>Evaluates with a fresh context so warnings can be asserted.</summary>
    public static (IDataTable Table, IReadOnlyList<string> Warnings) EvalWithWarnings(
        this IFlowNode node, IReadOnlyList<FlowValue> inputs, params (string Name, string Value)[] ps)
    {
        var ctx = new FakeEvalContext();
        var table = ((TableValue)node.Eval(ctx, inputs, Params(ps))[0]).Table;
        return (table, ctx.Warnings);
    }

    public static IReadOnlyList<object?> ColumnCells(this IDataTable table, string column)
    {
        var c = table.Columns.Single(c => c.Descriptor.Name == column);
        return Enumerable.Range(0, table.Rows.Count).Select(r => table[c.ColumnIndex, r]).ToList();
    }

    public static object? Cell(this IDataTable table, string column, int row)
        => table[table.Columns.Single(c => c.Descriptor.Name == column).ColumnIndex, row];

    public static IReadOnlyList<string> ColumnNames(this IDataTable table)
        => table.Columns.Select(c => c.Descriptor.Name).ToList();

    /// <summary>Walks up from the test output folder to the repo root and returns
    /// the sample file under samples/tables.</summary>
    public static string SamplePath(string fileName)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            var candidate = Path.Combine(dir.FullName, "samples", "tables", fileName);
            if (File.Exists(candidate))
                return candidate;
            dir = dir.Parent;
        }
        throw new FileNotFoundException($"samples/tables/{fileName} not found above {AppContext.BaseDirectory}");
    }
}

