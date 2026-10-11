using Ara3D.DataFlowEngine.Abstractions;

namespace BimOpenFlow.Nodes.DuckDb;

/// <summary>The bindable "path" parameter shared by the DuckDB readers: a file path typed inline, or
/// a Database value wired into the parameter's socket (usually from duck.source), which wins.</summary>
internal static class DuckSourceInput
{
    public const string Name = "path";

    public static ParamSpec Spec { get; } = new(Name, ParamKind.FilePath,
        Suggest: SuggestSource.FilesWith(".duckdb"), Bindable: PortType.Database);

    /// <summary>The Database value wired into the path socket, or null when the parameter holds its own string.</summary>
    public static DatabaseValue? Wired(ParamValues parameters)
        => parameters.GetBound(Name) as DatabaseValue;

    /// <summary>The wired source's path, or the required path string. The file must exist.</summary>
    public static string ResolvePath(ParamValues parameters, string kind)
    {
        var path = Wired(parameters)?.Path ?? parameters.RequiredText(Name, kind);
        if (!File.Exists(path))
            throw new FileNotFoundException($"{kind}: file not found: {path}", path);
        return path;
    }
}
