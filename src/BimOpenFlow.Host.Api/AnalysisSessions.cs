using Ara3D.DataFlowEngine;
using Ara3D.DataFlowEngine.Abstractions;
using Ara3D.NodeGraph;
using BimOpenFlow.Host.Store;

namespace BimOpenFlow.Host.Api;

/// <summary>
/// One standing EvalSession per analysis, created on demand from the store.
/// The engine is single-threaded by design, so every session operation runs
/// under a per-session lock; SSE fan-out uses the session's own observers.
/// </summary>
public sealed class AnalysisSessions
{
    private sealed class Entry
    {
        public readonly object Lock = new();
        public required EvalSession Session { get; init; }
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
    /// this analysis has no session yet.</summary>
    public EvalSnapshot Snapshot(string id)
    {
        var entry = GetOrCreate(id, loadOnCreate: true);
        lock (entry.Lock)
            return entry.Session.Snapshot;
    }

    /// <summary>Sets the (already validated) document as current and runs one pass.</summary>
    public EvalSnapshot Set(string id, GraphDocument doc)
    {
        var entry = GetOrCreate(id, loadOnCreate: false);
        lock (entry.Lock)
            return entry.Session.SetDocument(doc);
    }

    /// <summary>Observes every completed evaluation pass for one analysis.</summary>
    public IDisposable Subscribe(string id, Action<EvalSnapshot> observer)
    {
        var entry = GetOrCreate(id, loadOnCreate: true);
        lock (entry.Lock)
        {
            var subscription = entry.Session.Subscribe(observer);
            return new LockedDisposable(entry.Lock, subscription);
        }
    }

    // TODO: sessions never observe out-of-band edits to the store directory;
    // a stale session persists until the next PUT. Add an mtime check if needed.
    private Entry GetOrCreate(string id, bool loadOnCreate)
    {
        lock (_mapLock)
        {
            if (_entries.TryGetValue(id, out var existing))
                return existing;
            var entry = new Entry { Session = new EvalSession(_registry) };
            if (loadOnCreate)
                entry.Session.SetDocument(_store.Load(id));
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
