namespace BimOpenFlow.Relations.DuckDb;

public enum SourceType { DuckDbFile, FileRoot }

public readonly record struct SourceLocation(SourceType Type, string Path);

/// <summary>Resolves the source names a plan carries. Saved graphs hold only names, so
/// swapping the registry points the same graph at a different machine or a test fixture.</summary>
public interface IConnectionRegistry
{
    SourceLocation? Resolve(string name);
}

public sealed class ConnectionRegistry(IReadOnlyDictionary<string, SourceLocation> entries) : IConnectionRegistry
{
    public static ConnectionRegistry Of(params (string Name, SourceType Type, string Path)[] entries)
        => new(entries.ToDictionary(e => e.Name, e => new SourceLocation(e.Type, System.IO.Path.GetFullPath(e.Path))));

    public SourceLocation? Resolve(string name)
        => entries.TryGetValue(name, out var location) ? location : null;

    public IEnumerable<string> Names => entries.Keys;
}

public static class ConnectionRegistries
{
    public static SourceLocation Require(this IConnectionRegistry registry, string name)
        => registry.Resolve(name) ?? throw new ArgumentException($"Unknown source '{name}'.");
}
