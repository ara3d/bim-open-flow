using Ara3D.BimOpenSchema.DuckDb;
using Ara3D.DataTable;
using DuckDB.NET.Data;

namespace BimOpenFlow.Nodes.DuckDb;

/// <summary>Bounded, shared read-only database connections. Queries materialize under the lock;
/// eviction and disposal cannot close a connection while a branch is using it.</summary>
public sealed class DuckSourceCache(int capacity = 8) : IDisposable
{
    private sealed record Entry(DuckDBConnection Connection, long Length, DateTime Modified)
    {
        public long LastUse { get; set; }
    }

    public static DuckSourceCache Shared { get; } = new();
    static DuckSourceCache() => AppDomain.CurrentDomain.ProcessExit += (_, _) => Shared.Dispose();
    private readonly object gate = new();
    private readonly Dictionary<string, Entry> entries = new(OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
    private long clock;
    private bool disposed;
    public long OpenedConnections { get; private set; }

    public string Load(string path)
    {
        var fullPath = Path.GetFullPath(path);
        lock (gate) { Get(fullPath); }
        return fullPath;
    }

    public IDataTable Query(string source, string sql)
    {
        lock (gate) return Get(Path.GetFullPath(source)).Query(sql, "query").NormalizeDatesToText();
    }

    private DuckDBConnection Get(string path)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (capacity < 1) throw new ArgumentOutOfRangeException(nameof(capacity));
        var file = new FileInfo(path);
        if (!file.Exists) throw new FileNotFoundException($"duck.source: file not found: {path}", path);
        if (entries.TryGetValue(path, out var entry) && (entry.Length != file.Length || entry.Modified != file.LastWriteTimeUtc))
        {
            entry.Connection.Dispose();
            entries.Remove(path);
            entry = null;
        }
        if (entry is null)
        {
            if (entries.Count >= capacity)
            {
                var oldest = entries.MinBy(pair => pair.Value.LastUse);
                oldest.Value.Connection.Dispose();
                entries.Remove(oldest.Key);
            }
            var connection = DuckDbOps.OpenReadOnly(path);
            entry = new(connection, file.Length, file.LastWriteTimeUtc);
            entries.Add(path, entry);
            OpenedConnections++;
        }
        entry.LastUse = ++clock;
        return entry.Connection;
    }

    public void Dispose()
    {
        lock (gate)
        {
            foreach (var entry in entries.Values) entry.Connection.Dispose();
            entries.Clear();
            disposed = true;
        }
    }
}
