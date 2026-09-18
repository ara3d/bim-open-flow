using Ara3D.DataTable;

namespace BimOpenFlow.Relations.DuckDb;

/// <summary>Where the executor finds the rows behind an <see cref="InlineTable"/> plan node.</summary>
public interface IInlineTables
{
    IDataTable? Find(string hash);
}

/// <summary>The answer for a query that reads no inline tables.</summary>
public sealed class NoInlineTables : IInlineTables
{
    public static readonly IInlineTables Instance = new NoInlineTables();

    public IDataTable? Find(string hash) => null;
}

/// <summary>A bounded store of in-process tables keyed by a caller-supplied content hash.
/// Bounded by entry count so a graph that keeps producing tables cannot grow memory without
/// limit; the oldest entry goes first. Safe for concurrent use, as the host evaluates
/// several graphs at once.</summary>
public sealed class InlineTableStore(int capacity = 16) : IInlineTables
{
    private readonly int _capacity = capacity > 0 ? capacity
        : throw new ArgumentOutOfRangeException(nameof(capacity), "An inline table store holds at least one table.");
    private readonly Dictionary<string, IDataTable> _tables = new(StringComparer.Ordinal);
    private readonly Queue<string> _order = new();
    private readonly object _gate = new();

    public int Capacity => _capacity;

    public int Count
    {
        get { lock (_gate) return _tables.Count; }
    }

    public IDataTable? Find(string hash)
    {
        lock (_gate) return _tables.TryGetValue(hash, out var table) ? table : null;
    }

    /// <summary>Registers the table under the hash, evicting the oldest entry when full.
    /// Registering a hash the store already holds keeps the table it already has, which is
    /// the same table: the hash is the rows' identity.</summary>
    public void Add(string hash, IDataTable table)
    {
        lock (_gate)
        {
            if (!_tables.TryAdd(hash, table)) return;
            _order.Enqueue(hash);
            while (_order.Count > _capacity) _tables.Remove(_order.Dequeue());
        }
    }
}
