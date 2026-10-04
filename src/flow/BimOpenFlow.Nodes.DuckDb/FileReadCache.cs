using System.Collections.Concurrent;
using System.Security.Cryptography;
using Ara3D.BimOpenSchema.DuckDb;
using Ara3D.DataTable;

namespace BimOpenFlow.Nodes.DuckDb;

/// <summary>Content-hash caching shared by the file-reader nodes: results are
/// keyed by the hash of every matched file's bytes plus the parameter values,
/// so unchanged files never reload and any edit or new glob match reloads.</summary>
internal static class FileReadCache
{
    // TODO: unbounded cache; add eviction if long-lived hosts cycle through many files.
    private static readonly ConcurrentDictionary<string, IDataTable> Cache = new();

    public static bool IsGlob(string path)
        => path.IndexOfAny(['*', '?', '[']) >= 0;

    /// <summary>Resolves a path or glob to the matched files (DuckDB glob
    /// semantics): a plain path must exist, a glob must match at least one file.</summary>
    public static IReadOnlyList<string> ResolveFiles(string path, string kind)
    {
        if (!IsGlob(path))
            return File.Exists(path)
                ? [path]
                : throw new FileNotFoundException($"{kind}: file not found: {path}", path);
        using var conn = BosDuckDb.OpenInMemory();
        var matches = conn.Query($"SELECT file FROM glob('{path.ToSqlLiteral()}') ORDER BY file");
        var files = new List<string>();
        foreach (var row in matches.Rows)
        {
            var file = row[0]?.ToString();
            if (!string.IsNullOrEmpty(file))
                files.Add(file);
        }
        return files.Count > 0
            ? files
            : throw new FileNotFoundException($"{kind}: glob '{path}' matched no files.");
    }

    /// <summary>The cache key: kind + per-file (path, content hash) pairs +
    /// parameter values. Paths are part of the key because outputs embed
    /// path-derived data (the glob `filename` column, the table name), so
    /// identical bytes at a different location must not hit the cache.</summary>
    public static string CacheKey(string kind, IReadOnlyList<string> files, string parameters)
        => $"{kind}:{string.Join("|", files.Select(f => $"{f}={FileHashes.HashFile(f)}"))}:{parameters}";

    public static IDataTable GetOrLoad(string key, Func<IDataTable> load)
        => Cache.GetOrAdd(key, _ => load());
}
