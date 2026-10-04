namespace BimOpenFlow.Host.Catalog;

/// <summary>Thread-safe map holding at most Capacity entries, evicting the least
/// recently used. The factory runs outside the lock, so two callers racing on a
/// cold key may both build; the first stored value wins and both see it.</summary>
public sealed class BoundedCache<TKey, TValue> where TKey : notnull
{
    public readonly int Capacity;
    private readonly Dictionary<TKey, TValue> _values;
    private readonly LinkedList<TKey> _order = new();

    public BoundedCache(int capacity, IEqualityComparer<TKey>? comparer = null)
    {
        if (capacity < 1)
            throw new ArgumentOutOfRangeException(nameof(capacity));
        Capacity = capacity;
        _values = new(comparer);
    }

    public int Count
    {
        get { lock (_order) return _values.Count; }
    }

    public TValue GetOrAdd(TKey key, Func<TKey, TValue> create)
        => TryGet(key, out var found) ? found : Add(key, create(key));

    private bool TryGet(TKey key, out TValue value)
    {
        lock (_order)
        {
            if (!_values.TryGetValue(key, out value!))
                return false;
            Touch(key);
            return true;
        }
    }

    private TValue Add(TKey key, TValue value)
    {
        lock (_order)
        {
            if (_values.TryGetValue(key, out var existing))
            {
                Touch(key);
                return existing;
            }
            _values[key] = value;
            _order.AddFirst(key);
            while (_order.Count > Capacity)
            {
                _values.Remove(_order.Last!.Value);
                _order.RemoveLast();
            }
            return value;
        }
    }

    private void Touch(TKey key)
    {
        _order.Remove(key);
        _order.AddFirst(key);
    }
}
