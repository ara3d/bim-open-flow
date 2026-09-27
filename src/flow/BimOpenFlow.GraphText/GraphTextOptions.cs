using Ara3D.DataFlowEngine.Abstractions;
using Ara3D.DataTable;

namespace BimOpenFlow.GraphText;

/// <summary>Golden text is byte-stable across runs and machines: rounded numbers, aliased
/// paths, no engine hashes or execution counts. Debug adds those and prints numbers in full.</summary>
public enum GraphTextMode
{
    Golden,
    Debug,
}

/// <summary>An absolute directory printed as a placeholder, e.g. samples/nrc as {SAMPLES}.</summary>
public sealed record PathAlias(string Path, string Alias);

/// <summary>Reads a relation's rows for its digest, the way the host's result peek does:
/// a count, then a bounded page. The printer never materializes a relation without one.</summary>
public interface IRelationReader
{
    long Count(RelationValue relation);
    IDataTable Rows(RelationValue relation, long limit);
}

public sealed record GraphTextOptions
{
    public static readonly GraphTextOptions Golden = new();

    public GraphTextMode Mode { get; init; } = GraphTextMode.Golden;

    /// <summary>Printed in the header comment beside the graph hash.</summary>
    public string? AnalysisId { get; init; }

    public IReadOnlyList<PathAlias> PathAliases { get; init; } = [];

    /// <summary>Without a reader a relation prints its plan hash and text only.</summary>
    public IRelationReader? Relations { get; init; }

    public int SampleRows { get; init; } = 3;
    public int MaxColumns { get; init; } = 16;
    public int MaxListed { get; init; } = 40;

    /// <summary>A relation with more rows than this is sampled, not hashed.</summary>
    public long RelationRowCap { get; init; } = 10_000;

    /// <summary>Replaces every aliased directory, in either slash direction and any letter
    /// case, with its alias; longer paths first, so a nested alias wins over its parent.</summary>
    public string Scrub(string text)
    {
        foreach (var alias in PathAliases.OrderByDescending(a => a.Path.Length))
        {
            var full = System.IO.Path.GetFullPath(alias.Path).TrimEnd('\\', '/');
            text = text
                .Replace(full.Replace('\\', '/'), alias.Alias, StringComparison.OrdinalIgnoreCase)
                .Replace(full.Replace('/', '\\'), alias.Alias, StringComparison.OrdinalIgnoreCase);
        }
        return text;
    }
}
