namespace BimOpenFlow.Host.Store;

/// <summary>One analysis in the library. Name comes from an optional name.txt
/// sidecar in the analysis folder; it falls back to the id.</summary>
public sealed record AnalysisEntry(string Id, string Name);

/// <summary>One archived version of an analysis document.</summary>
public sealed record AnalysisVersion(int Sequence, string GraphHash, string FileName);
