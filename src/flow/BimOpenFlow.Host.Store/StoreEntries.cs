namespace BimOpenFlow.Host.Store;

/// <summary>One analysis in the library. Name comes from an optional name.txt
/// sidecar in the analysis folder; it falls back to the id.</summary>
public sealed record AnalysisEntry(string Id, string Name);

/// <summary>One archived version of an analysis document.</summary>
public sealed record AnalysisVersion(int Sequence, string GraphHash, string FileName);

/// <summary>Length and last-write ticks of a stored document: equal stamps mean
/// the same bytes for any writer that replaces the file atomically.</summary>
public readonly record struct StoreStamp(long Length, long LastWriteUtcTicks);
