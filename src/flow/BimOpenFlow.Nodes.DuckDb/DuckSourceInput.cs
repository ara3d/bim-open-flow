using Ara3D.DataFlowEngine.Abstractions;

namespace BimOpenFlow.Nodes.DuckDb;

/// <summary>The optional "source" input shared by the DuckDB nodes: a wired Database value wins
/// over the node's own path parameter.</summary>
internal static class DuckSourceInput
{
    public const string Port = "source";

    public static PortSpec Spec { get; } = new(Port, PortType.Database, Optional: true);

    /// <summary>The wired Database value, or null when the input is unconnected (Missing) or absent.</summary>
    public static DatabaseValue? Wired(IReadOnlyList<FlowValue> inputs)
        => inputs.Count > 0 ? inputs[0] as DatabaseValue : null;

    /// <summary>The wired source's path, warning when the path parameter is also set; otherwise the
    /// required path parameter. The file must exist.</summary>
    public static string ResolvePath(IEvalContext context, IReadOnlyList<FlowValue> inputs,
        ParamValues parameters, string kind)
    {
        var path = Wired(inputs) is { } source
            ? WarnIfBothSet(context, parameters, kind, source.Path)
            : parameters.RequiredText("path", kind);
        if (!File.Exists(path))
            throw new FileNotFoundException($"{kind}: file not found: {path}", path);
        return path;
    }

    /// <summary>Returns the source path after warning that the path parameter is ignored.</summary>
    public static string WarnIfBothSet(IEvalContext context, ParamValues parameters, string kind, string sourcePath)
    {
        var own = parameters.GetText("path");
        if (own.Length > 0)
            context.Warn($"{kind}: both the source input and the path parameter are set; using the source ({sourcePath}), ignoring path ({own}).");
        return sourcePath;
    }
}
