using Ara3D.DataTable;

namespace BimOpenFlow.Relations.DuckDb;

/// <summary>Where the executor finds the rows behind an <see cref="InlineTable"/> plan node.</summary>
public interface IInlineTables
{
    IDataTable? Find(string hash);
}

/// <summary>A bounded store of in-process tables keyed by a caller-supplied content hash.
/// Contract C4 of the nrc-handoff wave; the body belongs to track C.</summary>
public sealed class InlineTableStore(int capacity = 16) : IInlineTables
{
    public int Capacity => capacity;

    public IDataTable? Find(string hash)
        => throw new NotImplementedException("Track C fills in InlineTableStore.");

    /// <summary>Registers the table under the hash, evicting the least recently added when full.</summary>
    public void Add(string hash, IDataTable table)
        => throw new NotImplementedException("Track C fills in InlineTableStore.");
}
