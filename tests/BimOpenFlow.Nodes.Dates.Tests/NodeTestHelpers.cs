using Ara3D.DataFlowEngine.Abstractions;
using Ara3D.DataTable;

namespace BimOpenFlow.Nodes.Dates.Tests;

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

