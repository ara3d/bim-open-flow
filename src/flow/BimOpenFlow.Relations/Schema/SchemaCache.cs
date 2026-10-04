namespace BimOpenFlow.Relations;

/// <summary>Schema inference memoized by plan hash, so editing one node re-infers only it
/// and the nodes above it. One instance per catalog; not thread-safe.</summary>
public sealed class SchemaCache(ICatalog catalog)
{
    private readonly Dictionary<string, SchemaResult> _results = new();

    public int Count => _results.Count;

    public SchemaResult Infer(Plan plan)
    {
        if (_results.TryGetValue(plan.Hash, out var cached)) return cached;
        var inputs = plan.Inputs.Select(Infer).ToList();
        var result = SchemaInference.InferNode(plan, inputs, catalog);
        _results[plan.Hash] = result;
        return result;
    }

    public void Clear()
        => _results.Clear();
}
