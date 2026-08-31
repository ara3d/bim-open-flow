using Ara3D.DataFlowEngine.Abstractions;
using Ara3D.DataTable;

namespace BimOpenFlow.Nodes.Effects;

/// <summary>Shared plumbing for the sink nodes: run gating, input access, summary tables.</summary>
internal static class Sinks
{
    /// <summary>Defense in depth: the engine already refuses to evaluate Effect nodes outside a Run.</summary>
    public static void RequireRun(this IEvalContext context, string kind)
    {
        if (!context.IsRun)
            throw new InvalidOperationException($"'{kind}' is an Effect node and can only execute inside a Run");
    }

    public static IDataTable TableAt(this IReadOnlyList<FlowValue> inputs, int index)
        => inputs.Count > index && inputs[index] is TableValue t
            ? t.Table
            : throw new ArgumentException($"Input {index} must be a Table");

    public static string RequiredPath(this ParamValues parameters, string name)
        => parameters.GetText(name) is { Length: > 0 } path
            ? path
            : throw new ArgumentException($"Parameter '{name}' must be a non-empty file path");

    public static int RequiredColumn(this IDataTable table, string name)
    {
        for (var i = 0; i < table.Columns.Count; i++)
            if (table.Columns[i].Descriptor.Name == name)
                return i;
        throw new ArgumentException($"Table '{table.Name}' has no '{name}' column");
    }

    /// <summary>A one-row summary table; long cells become Integer columns, everything else Text.</summary>
    public static IDataTable SummaryRow(string name, params (string Name, object Value)[] cells)
    {
        var columns = new MemoryColumn[cells.Length];
        for (var i = 0; i < cells.Length; i++)
            columns[i] = new MemoryColumn(
                cells[i].Name,
                cells[i].Value is long ? typeof(long) : typeof(string),
                new[] { (object?)cells[i].Value },
                i);
        return new MemoryTable(name, columns);
    }

    /// <summary>Writes text to a path, creating the parent directory when needed.</summary>
    public static void WriteAllText(string path, string text)
    {
        var dir = Path.GetDirectoryName(Path.GetFullPath(path));
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);
        File.WriteAllText(path, text);
    }
}
