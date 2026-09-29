using Ara3D.MCP;

namespace BimOpenMcp.Flow;

/// <summary>Read-only tools over the repository's own documents (see RepoDocs), so an agent can
/// answer questions about the toolkit itself: what it is, how to run it, which demos, nodes and
/// tickets exist. They never write and never reach a file outside the allowlist.</summary>
public static class FlowRepoDocTools
{
    public const int DefaultHits = 10;
    public const int MaxHits = 50;
    public const int LinesPerHit = 3;
    public const int DefaultChars = 12_000;
    public const int MaxChars = 40_000;

    public static McpServer RegisterRepoDocTools(this McpServer mcp, RepoDocs? docs)
        => mcp
            .Tool(
                "searchDocs",
                "Searches the toolkit's own documents (README.md, PROJECT.md, docs/**, samples/*/README.md, "
                + "tickets/**) for words, case-insensitively. Returns the best-matching files with their titles and "
                + "matching lines. Use it for questions about the toolkit rather than the building model.",
                McpSchema.Object()
                    .String("query", "Words to look for, e.g. 'NRC walkthrough' or 'status: open'.", required: true)
                    .String("under", "Only paths starting with this, e.g. 'tickets/' or 'docs/'.")
                    .Integer("take", $"Files to return. Default {DefaultHits}, capped at {MaxHits}.")
                    .Build(),
                (args, _) => ToolRunner.RunAsync(
                    () => Search(Require(docs), args.GetRequiredString("query"), args.GetString("under"),
                        Math.Clamp(args.GetInt("take") ?? DefaultHits, 1, MaxHits)),
                    ["readDoc"]))
            .Tool(
                "readDoc",
                "Reads one of the toolkit's documents by its path relative to the repository root (as searchDocs "
                + "returns it). Long documents are paged: pass 'offset' from the previous call's 'nextOffset'.",
                McpSchema.Object()
                    .String("path", "e.g. 'README.md', 'docs/OVERVIEW.md', 'samples/nrc/README.md'.", required: true)
                    .Integer("offset", "Character offset to start from. Default 0.")
                    .Integer("maxChars", $"Characters to return. Default {DefaultChars}, capped at {MaxChars}.")
                    .Build(),
                (args, _) => ToolRunner.RunAsync(
                    () => Read(Require(docs), args.GetRequiredString("path"), Math.Max(args.GetInt("offset") ?? 0, 0),
                        Math.Clamp(args.GetInt("maxChars") ?? DefaultChars, 1, MaxChars)),
                    ["searchDocs"]));

    /// <summary>Files ranked by how many distinct query words they contain (a word in the path counts
    /// twice, the whole query as a phrase once more), each with its title and first matching lines.</summary>
    public static object Search(RepoDocs docs, string query, string? under = null, int take = DefaultHits)
    {
        var words = query.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(w => w.ToLowerInvariant()).Distinct().ToList();
        if (words.Count == 0)
            throw new ArgumentException("Give at least one word to search for.");
        var phrase = string.Join(' ', words);
        var prefix = under?.Replace('\\', '/').TrimStart('/') ?? "";
        var ranked = docs.Files()
            .Where(path => path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            .Select(path => (path, text: File.ReadAllText(Path.Combine(docs.Root, path))))
            .Select(f => (f.path, f.text, score: Score(f.path, f.text.ToLowerInvariant(), words, phrase)))
            .Where(f => f.score > 0)
            .OrderByDescending(f => f.score)
            .ThenBy(f => f.path, StringComparer.Ordinal)
            .ToList();
        return new
        {
            total = ranked.Count,
            files = ranked.Take(take).Select(f => new
            {
                path = f.path,
                title = Title(f.text),
                lines = MatchingLines(f.text, words),
            }).ToList(),
        };
    }

    public static object Read(RepoDocs docs, string path, int offset = 0, int maxChars = DefaultChars)
    {
        var full = docs.FullPath(path);
        var text = File.ReadAllText(full);
        var start = Math.Min(offset, text.Length);
        var length = Math.Min(maxChars, text.Length - start);
        var end = start + length;
        return new
        {
            path = RepoDocs.Allowed(path),
            totalChars = text.Length,
            offset = start,
            text = text.Substring(start, length),
            nextOffset = end < text.Length ? end : (int?)null,
        };
    }

    private static RepoDocs Require(RepoDocs? docs)
        => docs ?? throw new InvalidOperationException(
            "The toolkit's documents are not available: this server is not running from a repository checkout.");

    private static int Score(string path, string lowerText, IReadOnlyList<string> words, string phrase)
    {
        var inText = words.Count(lowerText.Contains);
        if (inText == 0)
            return 0;
        var inPath = words.Count(w => path.Contains(w, StringComparison.OrdinalIgnoreCase));
        return inText + 2 * inPath + (words.Count > 1 && lowerText.Contains(phrase) ? 1 : 0);
    }

    /// <summary>The frontmatter 'title:' line, else the first '# ' heading, else null.</summary>
    private static string? Title(string text)
    {
        var lines = text.Split('\n').Select(l => l.TrimEnd('\r')).ToList();
        return lines.FirstOrDefault(l => l.StartsWith("title:", StringComparison.Ordinal))?[6..].Trim()
            ?? lines.FirstOrDefault(l => l.StartsWith("# ", StringComparison.Ordinal))?[2..].Trim();
    }

    private static IReadOnlyList<object> MatchingLines(string text, IReadOnlyList<string> words)
        => text.Split('\n')
            .Select((line, i) => (line: line.TrimEnd('\r'), number: i + 1))
            .Where(l => words.Any(w => l.line.Contains(w, StringComparison.OrdinalIgnoreCase)))
            .Take(LinesPerHit)
            .Select(l => (object)new { l.number, text = l.line.Length <= 200 ? l.line : l.line[..200] + "…" })
            .ToList();
}
