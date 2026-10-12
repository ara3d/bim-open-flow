using Ara3D.DataFlowEngine;
using Ara3D.DataFlowEngine.Abstractions;
using Ara3D.NodeGraph;
using BimOpenFlow.Contracts;
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
    private readonly ISchemaCatalog _schemaCatalog;
    private readonly SchemaPassCache _schemaPasses = new();
    private readonly object _mapLock = new();
    private readonly Dictionary<string, Entry> _entries = new();

    /// <summary>schemaCatalog answers the schema pass's questions about files and SQL; without one, a node
    /// that needs it gets an error naming the source (a StaticCatalog with no entries).</summary>
    public AnalysisSessions(AnalysisStore store, INodeRegistry registry, ISchemaCatalog? schemaCatalog = null)
    {
        _store = store;
        _registry = registry;
        _schemaCatalog = schemaCatalog ?? new StaticCatalog(new Dictionary<string, Schema>());
    }

    /// <summary>The schema pass over this snapshot's document and graph-input values, cached until either changes.
    /// No node is evaluated.</summary>
    public IReadOnlyList<PortSchema> Schemas(string id, EvalSnapshot snapshot)
        => Schemas(id, snapshot.Document, snapshot.GraphInputs ?? new Dictionary<string, string>());

    private IReadOnlyList<PortSchema> Schemas(string id, GraphDocument doc, IReadOnlyDictionary<string, string> given)
    {
        var resolved = doc.Resolve(given, []);
        return _schemaPasses.Get(id, doc, resolved, () => GraphSchemas
            .Infer(doc, _registry, _schemaCatalog, resolved)
            .Select(p => p.ToPortSchema()).ToList());
    }

    /// <summary>The update for one snapshot, with its schemas.</summary>
    public EvalUpdate Update(string id, EvalSnapshot snapshot)
        => snapshot.ToEvalUpdate(id, Schemas(id, snapshot));

    /// <summary>The schema pass over the analysis as stored, with its stored graph-input values. Evaluates
    /// nothing, so it is cheap on a large model and does not wait for a session's evaluation.</summary>
    public AnalysisSchemas Schemas(string id)
    {
        var doc = _store.Load(id);
        var held = HeldInputs(id, doc);
        return new(id, doc.ComputeGraphHash(), Schemas(id, doc, held));
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

    /// <summary>The engine Run over the analysis's current document (spec semantics §6): effect
    /// nodes execute once, in order, and the run snapshot becomes the session's current one, so
    /// subscribers see the effects' outputs. Reloads first if the store changed underneath.</summary>
    public EvalSnapshot Run(string id, CancellationToken ct = default)
    {
        var entry = GetOrCreate(id);
        lock (entry.Lock)
        {
            Refresh(entry, id);
            return entry.Session.Run(ct);
        }
    }

    /// <summary>Sets the (already validated and saved) document as current and runs one pass.</summary>
    public EvalSnapshot Set(string id, GraphDocument doc)
    {
        var entry = GetOrCreate(id);
        lock (entry.Lock)
        {
            var snapshot = entry.Session.SetDocument(doc, HeldInputs(id, doc));
            entry.Stamp = _store.Stamp(id);
            return snapshot;
        }
    }

    /// <summary>Merges these values into the analysis's stored graph-input values, evaluates the
    /// document with them, and keeps them. A name the document does not declare or a value its kind
    /// rejects throws InvalidGraphException, and neither the store nor the session changes.</summary>
    public EvalSnapshot SetGraphInputs(string id, IReadOnlyDictionary<string, string> values)
    {
        var entry = GetOrCreate(id);
        lock (entry.Lock)
        {
            Refresh(entry, id);
            var merged = HeldInputs(id, entry.Session.Document)
                .Concat(values)
                .GroupBy(kv => kv.Key)
                .ToDictionary(g => g.Key, g => g.Last().Value);
            var snapshot = entry.Session.SetGraphInputs(merged);
            _store.SaveGraphInputs(id, merged);
            entry.Stamp = _store.Stamp(id);
            return snapshot;
        }
    }

    private IReadOnlyDictionary<string, string> HeldInputs(string id, GraphDocument doc)
        => GraphInputValues.Held(doc, _store.LoadGraphInputs(id));

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

    /// <summary>Evaluates every stored analysis once, so the caches behind them (IFC
    /// conversion, meshing, DuckDB attachments) are warm before a person opens one. Meant for
    /// a background task after the port is open; an analysis that fails is logged and skipped.</summary>
    public void WarmAll(TextWriter log)
    {
        var entries = _store.List();
        var started = System.Diagnostics.Stopwatch.StartNew();
        log.WriteLine($"  warming {entries.Count} analyses in the background");
        foreach (var entry in entries)
        {
            try
            {
                Snapshot(entry.Id);
            }
            catch (Exception e) when (e is not OutOfMemoryException)
            {
                log.WriteLine($"  warm-up skipped {entry.Id}: {e.Message}");
            }
        }
        log.WriteLine($"  warm-up done in {started.Elapsed.TotalSeconds:F1} s");
    }

    /// <summary>Re-runs every open session over its current document, for when the data
    /// behind the nodes changed while the documents did not (a source prepared in the
    /// background). Successful pure results stay memoized; failed nodes run again, and
    /// observers see the pass like any other.</summary>
    public void Reevaluate()
    {
        _schemaPasses.Clear();
        List<Entry> entries;
        lock (_mapLock)
            entries = _entries.Values.ToList();
        foreach (var entry in entries)
            lock (entry.Lock)
                entry.Session.SetDocument(entry.Session.Document);
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
        var doc = _store.Load(id);
        entry.Session.SetDocument(doc, HeldInputs(id, doc));
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
