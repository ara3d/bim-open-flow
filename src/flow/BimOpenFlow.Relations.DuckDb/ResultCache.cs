using Ara3D.DataTable;

namespace BimOpenFlow.Relations.DuckDb;

/// <summary>Materialized results keyed by plan hash and limit. Bounded by entry count so
/// inspecting many nodes cannot grow memory without limit; the oldest entry goes first.</summary>
public sealed class ResultCache(int capacity = 64)
{
    private readonly Dictionary<string, IDataTable> _tables = new();
    private readonly Queue<string> _order = new();
    private readonly object _gate = new();

    public int Count => _tables.Count;

    public static string Key(Plan plan, long? limit, long offset)
        => $"{plan.Hash}:{limit?.ToString() ?? "all"}:{offset}";

    public IDataTable GetOrAdd(Plan plan, long? limit, long offset, Func<IDataTable> materialize)
    {
        var key = Key(plan, limit, offset);
        lock (_gate)
            if (_tables.TryGetValue(key, out var hit)) return hit;
        var table = materialize();
        lock (_gate)
        {
            if (_tables.TryAdd(key, table))
            {
                _order.Enqueue(key);
                while (_order.Count > capacity) _tables.Remove(_order.Dequeue());
            }
            return _tables[key];
        }
    }

    public void Clear()
    {
        lock (_gate)
        {
            _tables.Clear();
            _order.Clear();
        }
    }
}
