using System.Security.Cryptography;
using Ara3D.BimOpenSchema.IO;
using Ara3D.Utils;

namespace BimOpenFlow.Host.Catalog;

/// <summary>Discovers model files (.ifc, .bos) under one or more root directories
/// and converts IFC sources to BOS in a cache directory keyed by source content
/// hash, so re-conversion happens only when a source changes.</summary>
public sealed class ModelCatalog
{
    public readonly IReadOnlyList<string> Roots;
    public readonly string CacheDir;
    private readonly IIfcConverter _converter;

    public ModelCatalog(IReadOnlyList<string> roots, string cacheDir, IIfcConverter? converter = null)
    {
        if (roots.Count == 0)
            throw new ArgumentException("At least one root directory is required.", nameof(roots));
        Roots = roots.Select(Path.GetFullPath).ToList();
        CacheDir = Path.GetFullPath(cacheDir);
        _converter = converter ?? new IfcToBosFileConverter();
    }

    public ModelCatalog(string root, string cacheDir, IIfcConverter? converter = null)
        : this([root], cacheDir, converter)
    {
    }

    /// <summary>Pure discovery: walks the roots and returns what is there now.
    /// Missing roots contribute nothing. Deterministic order (per root, then
    /// ordinal by path).</summary>
    // TODO: no file watchers in v1 — the host polls by calling Scan again.
    // TODO: memoize content hashing by (path, size, mtime) if scans get slow on large trees.
    public IReadOnlyList<ModelEntry> Scan()
    {
        var entries = new List<ModelEntry>();
        var seenIds = new HashSet<string>();
        foreach (var root in Roots)
        {
            if (!Directory.Exists(root))
                continue;
            var files = Directory
                .EnumerateFiles(root, "*", SearchOption.AllDirectories)
                .Where(f => KindOf(f) != null)
                .OrderBy(f => f, StringComparer.Ordinal);
            foreach (var file in files)
                entries.Add(CreateEntry(root, file, seenIds));
        }
        return entries;
    }

    /// <summary>Path to the BOS form of the model: the source itself for Bos
    /// entries; for Ifc entries a cached conversion at CacheDir/{hash}.bos.</summary>
    public string GetBos(ModelEntry entry)
        => entry.Kind == ModelKind.Bos ? entry.SourcePath : ConvertToCache(entry);

    /// <summary>Table sizes of the BOS form (converts first if needed).</summary>
    public ModelInfo GetInfo(ModelEntry entry)
    {
        var data = new FilePath(GetBos(entry)).ReadBimDataFromParquetZip();
        return new(data.Entities.Length, data.Parameters.Length, data.Documents.Length, data.Relations.Length);
    }

    public static ModelKind? KindOf(string path)
        => Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".ifc" => ModelKind.Ifc,
            ".bos" => ModelKind.Bos,
            _ => null,
        };

    private static ModelEntry CreateEntry(string root, string file, HashSet<string> seenIds)
    {
        var info = new FileInfo(file);
        var hash = HashFile(file);
        var id = Slug(Path.GetRelativePath(root, file));
        if (!seenIds.Add(id))
        {
            id = $"{id}-{hash[..8]}";
            seenIds.Add(id);
        }
        return new(
            id,
            Path.GetFileNameWithoutExtension(file),
            file,
            KindOf(file)!.Value,
            info.Length,
            hash,
            info.LastWriteTimeUtc);
    }

    /// <summary>Lowercased root-relative path with every non [a-z0-9.] run
    /// collapsed to a single '-': "Models\A 1.ifc" becomes "models-a-1.ifc".</summary>
    public static string Slug(string relativePath)
        => string.Join('-', relativePath
            .ToLowerInvariant()
            .Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .SelectMany(part => part.Split(' ', '_'))
            .Where(part => part.Length > 0)
            .Select(part => string.Concat(part.Where(c => char.IsAsciiLetterOrDigit(c) || c is '.' or '-'))));

    public static string HashFile(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }

    /// <summary>Concurrent-caller safe without a lock file: convert into a
    /// uniquely named temp file, then rename onto the final path. The first
    /// rename wins; a loser sees the target exist and discards its temp.</summary>
    private string ConvertToCache(ModelEntry entry)
    {
        Directory.CreateDirectory(CacheDir);
        var target = Path.Combine(CacheDir, entry.ContentHash + ".bos");
        if (File.Exists(target))
            return target;

        // TODO: stale cache entries (hashes no source maps to any more) are never evicted.
        var temp = Path.Combine(CacheDir, $"{entry.ContentHash}.{Guid.NewGuid():N}.tmp");
        try
        {
            _converter.Convert(entry.SourcePath, temp);
            File.Move(temp, target);
        }
        catch (IOException) when (File.Exists(target))
        {
            // A concurrent conversion won the rename; its result is identical.
        }
        finally
        {
            TryDelete(temp);
        }
        return target;
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch (IOException)
        {
        }
    }
}
