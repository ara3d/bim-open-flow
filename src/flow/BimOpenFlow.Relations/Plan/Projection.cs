using System.Text.RegularExpressions;

namespace BimOpenFlow.Relations;

/// <summary>One projection term: a column, optionally renamed. <c>Alias</c> is null for a plain name,
/// which keeps the column's own name. The one parser for <c>name</c> and <c>name as Alias</c>, shared by
/// rel.select and table.project.</summary>
public readonly partial record struct Projection(string Column, string? Alias = null)
{
    /// <summary>The name the column has in the output, as written: the alias if there is one.</summary>
    public string OutputName => Alias ?? Column;

    /// <summary>Parses <c>name</c> or <c>name as Alias</c> (case-insensitive <c>as</c>, any whitespace around it).
    /// The alias is kept as written. An <c>as</c> with nothing after it is part of the name.</summary>
    public static Projection Parse(string term)
    {
        var text = term.Trim();
        var m = AliasPattern().Match(text);
        return m.Success ? new Projection(m.Groups["column"].Value.Trim(), m.Groups["alias"].Value.Trim()) : new Projection(text);
    }

    public static IReadOnlyList<Projection> ParseAll(IEnumerable<string> terms)
        => terms.Select(Parse).ToList();

    /// <summary>The first output name that appears twice (ignoring case), or null.</summary>
    public static string? FirstDuplicate(IEnumerable<string> outputNames)
        => outputNames.GroupBy(n => n, StringComparer.OrdinalIgnoreCase).FirstOrDefault(g => g.Count() > 1)?.First();

    [GeneratedRegex(@"^(?<column>.*\S)\s+as\s+(?<alias>\S.*)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex AliasPattern();
}
