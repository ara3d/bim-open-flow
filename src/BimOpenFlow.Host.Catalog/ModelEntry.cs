namespace BimOpenFlow.Host.Catalog;

public enum ModelKind
{
    Ifc,
    Bos,
}

/// <summary>One discovered model file. Id is a slug of the root-relative path
/// (collisions across roots get a content-hash suffix), so it survives restarts.
/// ContentHash is the full lowercase SHA-256 of the file, computed at scan time.</summary>
public sealed record ModelEntry(
    string Id,
    string Name,
    string SourcePath,
    ModelKind Kind,
    long SizeBytes,
    string ContentHash,
    DateTime LastWriteUtc);
