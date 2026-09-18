namespace BimOpenFlow.Relations.DuckDb;

/// <summary>Wraps a registry with knowledge of sources that exist by name but are still being
/// prepared (a database being built in the background). An unresolved name the preparer
/// vouches for fails with a message that says so, instead of the generic "Unknown source",
/// so a graph opened before its data is ready shows why and that it will recover.</summary>
public sealed class PreparingRegistry(IConnectionRegistry inner, Func<string, string?> preparingReason) : IConnectionRegistry
{
    public SourceLocation? Resolve(string name)
        => inner.Resolve(name) ?? (preparingReason(name) is { } why ? throw new SourcePreparingException(name, why) : null);
}

/// <summary>Thrown when a query names a source that is being prepared; caught by the catalog
/// and executor like any other resolution failure, so it surfaces as the node's error text.</summary>
public sealed class SourcePreparingException(string source, string why)
    : InvalidOperationException($"Source '{source}' is not ready yet: {why}")
{
    public string Source { get; } = source;
}
