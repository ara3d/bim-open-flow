namespace BimOpenFlow.Relations.DuckDb;

/// <summary>A registry over model roots that rescans them on every lookup, so a database
/// written into a root after start-up (a sample prepared in the background, a file the
/// user drops in) resolves on its next use. A scan is one directory listing per root, far
/// cheaper than the schema inference or query that follows, and SchemaCache absorbs repeats.
/// Naming follows SourceRegistries.FromRoots.</summary>
public sealed class RootScanRegistry(IReadOnlyList<string> roots) : IConnectionRegistry
{
    public IReadOnlyList<string> Roots { get; } = roots;

    public SourceLocation? Resolve(string name)
        => Snapshot().Resolve(name);

    public IEnumerable<string> Names
        => Snapshot().Names;

    /// <summary>The registry as the roots stand right now.</summary>
    public ConnectionRegistry Snapshot()
        => SourceRegistries.FromRoots(Roots);
}
