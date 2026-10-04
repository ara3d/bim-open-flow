namespace BimOpenFlow.Host.Catalog;

public enum ModelKind
{
    Ifc,
    Bos,
}

/// <summary>One discovered model file. Id is a slug of the root-relative path
/// (collisions across roots get a content-hash suffix), so it survives restarts.
/// ContentHash is the full lowercase SHA-256 of the file, computed the first time it is
/// asked for (a conversion, a run record, an id collision), never by the scan itself, so
/// listing a root of large models costs one stat per file.</summary>
public sealed record ModelEntry(
    string Id,
    string Name,
    string SourcePath,
    ModelKind Kind,
    long SizeBytes,
    Lazy<string> Hash,
    DateTime LastWriteUtc)
{
    public string ContentHash => Hash.Value;
}
