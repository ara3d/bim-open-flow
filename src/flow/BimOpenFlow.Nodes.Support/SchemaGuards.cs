using Ara3D.DataFlowEngine.Abstractions;

namespace BimOpenFlow.Nodes.Support;

/// <summary>What the reader nodes share when they infer a schema: a path that cannot be read yet
/// (empty, or a graph input reference that has no value) gives an unknown schema with a message,
/// and an exception raised while reading parameters becomes a failed result, never a throw.</summary>
public static class SchemaGuards
{
    /// <summary>A failed result naming the node when the path is empty, is an unresolved
    /// <c>$name</c> graph input reference, or (unless it is a glob) names no file; else null.</summary>
    public static SchemaResult? PathProblem(string kind, string path, bool allowGlob = false)
    {
        if (string.IsNullOrWhiteSpace(path))
            return SchemaResult.Fail($"{kind}: the path is empty, so the columns are not known yet.");
        if (path.StartsWith('$') && !path.StartsWith("$$", StringComparison.Ordinal))
            return SchemaResult.Fail($"{kind}: the path is the graph input {path}, which has no value here, so the columns are not known yet.");
        if (allowGlob && path.IndexOfAny(['*', '?', '[']) >= 0)
            return null;
        return File.Exists(path) ? null : SchemaResult.Fail($"{kind}: file not found: {path}");
    }

    /// <summary>Runs an inference that reads parameters; a bad parameter value becomes the failed result.</summary>
    public static SchemaResult Guard(Func<SchemaResult> infer)
    {
        try
        {
            return infer();
        }
        catch (Exception e) when (e is ArgumentException or FormatException or OverflowException or FileNotFoundException)
        {
            return SchemaResult.Fail(e.Message);
        }
    }
}
