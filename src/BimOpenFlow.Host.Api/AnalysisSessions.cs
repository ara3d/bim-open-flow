using Ara3D.DataFlowEngine;
using Ara3D.DataFlowEngine.Abstractions;
using Ara3D.NodeGraph;
using BimOpenFlow.Host.Store;

namespace BimOpenFlow.Host.Api;

/// <summary>
/// One standing EvalSession per analysis, created on demand from the store.
/// The engine is single-threaded by design, so every session operation runs
/// under a per-session lock; SSE fan-out uses the session's own observers.
/// A session remembers the store stamp of the document it holds and reloads
/// when the file on disk has been replaced by another writer (the MCP tools,
/// another process), so readers never see results of a superseded document.
/// </summary>
public sealed class AnalysisSessions
{
    private sealed class Entry
    {
        public readonly object Lock = new();
        public required EvalSession Session { get; init; }
        public StoreStamp? Stamp;
    }

    private readonly AnalysisStore _store;
    private readonly INodeRegistry _registry;
    private readonly object _mapLock = new();
    private readonly Dictionary<string, Entry> _entries = new();

    public AnalysisSessions(AnalysisStore store, INodeRegistry registry)
    {
        _store = store;
        _registry = registry;
    }

    /// <summary>The current snapshot, evaluating the stored document first if
    /// this analysis has no session yet or the store has changed underneath it.</summary>
    public EvalSnapshot Snapshot(string id)
    {
        var entry = GetOrCreate(id);
        lock (entry.Lock)
        {
            Refresh(entry, id);
            return entry.Session.Snapshot;
        }
    }

    /// <summary>Sets the (already validated and saved) document as current and runs one pass.</summary>
    public EvalSnapshot Set(string id, GraphDocument doc)
    {
        var entry = GetOrCreate(id);
        lock (entry.Lock)
        {
            var snapshot = entry.Session.SetDocument(doc);
            entry.Stamp = _store.Stamp(id);
            return snapshot;
        }
    }

    /// <summary>Observes every completed evaluation pass for one analysis.</summary>
    public IDisposable Subscribe(string id, Action<EvalSnapshot> observer)
    {
        var entry = GetOrCreate(id);
        lock (entry.Lock)
        {
            Refresh(entry, id);
            var subscription = entry.Session.Subscribe(observer);
            return new LockedDisposable(entry.Lock, subscription);
        }
    }

    /// <summary>Reloads when the stored bytes differ from the ones this session
    /// evaluated. Cheap: one stat call, no read, when nothing changed.</summary>
    private void Refresh(Entry entry, string id)
    {
        var current = _store.Stamp(id);
        if (current is null && entry.Stamp is null)
            throw new FileNotFoundException($"Analysis '{id}' not found");
        if (current is null || current == entry.Stamp)
            return;
        entry.Session.SetDocument(_store.Load(id));
        entry.Stamp = current;
    }

    private Entry GetOrCreate(string id)
    {
        lock (_mapLock)
        {
            if (_entries.TryGetValue(id, out var existing))
                return existing;
            var entry = new Entry { Session = new EvalSession(_registry) };
            _entries[id] = entry;
            return entry;
        }
    }

    private sealed class LockedDisposable(object gate, IDisposable inner) : IDisposable
    {
        public void Dispose()
        {
            lock (gate)
                inner.Dispose();
        }
    }
}
