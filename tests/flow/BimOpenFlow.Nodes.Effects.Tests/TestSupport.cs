using Ara3D.DataFlowEngine.Abstractions;
using Ara3D.DataTable;
using BimOpenFlow.Nodes.Effects;

namespace BimOpenFlow.Nodes.Effects.Tests;

internal sealed class FakeContext : IEvalContext
{
    public static readonly FakeContext Run = new(true);
    public static readonly FakeContext Design = new(false);

    public bool IsRun { get; }
    public CancellationToken Cancellation => CancellationToken.None;
    public List<string> Warnings { get; } = new();

    public FakeContext(bool isRun)
        => IsRun = isRun;

    public void Warn(string message)
        => Warnings.Add(message);
}

internal static class TestSupport
{
    /// <summary>A 3-row table exercising quoting, nulls, and every scalar kind.</summary>
    public static IDataTable FixtureTable()
        => new MemoryTable("fixture", new[]
        {
            new MemoryColumn("name", typeof(string), new object?[] { "plain", "with, comma", "with \"quote\"\nand newline" }, 0),
            new MemoryColumn("count", typeof(long), new object?[] { 1L, null, 3L }, 1),
            new MemoryColumn("ratio", typeof(double), new object?[] { 0.5, 2.25, null }, 2),
            new MemoryColumn("flag", typeof(bool), new object?[] { true, false, null }, 3),
        });

    public static FlowValue[] TableInput(IDataTable table)
        => new FlowValue[] { new TableValue(table) };

    public static ParamValues Params(params (string Name, string Value)[] pairs)
        => new(pairs.ToDictionary(p => p.Name, p => p.Value));

    public static IDataTable OutputTable(IReadOnlyList<FlowValue> outputs)
        => ((TableValue)outputs[0]).Table;

    public static object? Cell(IDataTable table, string column, int row = 0)
    {
        for (var i = 0; i < table.Columns.Count; i++)
            if (table.Columns[i].Descriptor.Name == column)
                return table[i, row];
        throw new ArgumentException($"No column '{column}'");
    }

    public static string NewTempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "bof-effects-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }

    /// <summary>Best-effort: the STEP parser memory-maps IFC files and can hold a lock briefly.</summary>
    public static void DeleteTempDir(string dir)
    {
        if (!Directory.Exists(dir))
            return;
        try
        {
            Directory.Delete(dir, true);
        }
        catch (IOException)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            try
            {
                Directory.Delete(dir, true);
            }
            catch (IOException)
            {
            }
        }
    }
}
