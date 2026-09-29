using BimOpenFlow.Host;

namespace BimOpenMcp.Flow;

/// <summary>The repository documents an agent may read: README.md, PROJECT.md, docs/**,
/// samples/*/README.md and tickets/**, as text files (.md, .txt, .json) outside any bin or obj
/// folder. Paths are relative with '/' separators; an absolute path, a '.' or '..' segment, or
/// anything outside the allowlist is refused, so the tools can never reach data/, src/, or the
/// private model files.</summary>
public sealed class RepoDocs
{
    public static readonly IReadOnlyList<string> TextExtensions = [".md", ".txt", ".json"];
    public static readonly IReadOnlyList<string> RootFiles = ["README.md", "PROJECT.md"];
    public static readonly IReadOnlyList<string> Folders = ["docs", "tickets"];

    public readonly string Root;

    public RepoDocs(string root)
        => Root = Path.GetFullPath(root);

    /// <summary>The checkout the running binary was built from (the host runs from bin/), else the
    /// one containing the working directory; null for an installed copy with no checkout.</summary>
    public static RepoDocs? Locate()
        => (SampleSeeding.FindRepoRoot(AppContext.BaseDirectory)
                ?? SampleSeeding.FindRepoRoot(Environment.CurrentDirectory)) is { } root
            ? new RepoDocs(root)
            : null;

    /// <summary>The normalised relative path when it is on the allowlist, else null.</summary>
    public static string? Allowed(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || Path.IsPathRooted(path) || path.Contains(':'))
            return null;
        var segments = path.Replace('\\', '/').Split('/');
        if (segments.Any(s => s is "" or "." or ".." || s.Equals("bin", StringComparison.OrdinalIgnoreCase)
                || s.Equals("obj", StringComparison.OrdinalIgnoreCase)))
            return null;
        if (!TextExtensions.Contains(Path.GetExtension(path).ToLowerInvariant()))
            return null;
        var relative = string.Join('/', segments);
        var ok = segments.Length switch
        {
            1 => RootFiles.Any(f => f.Equals(relative, StringComparison.OrdinalIgnoreCase)),
            _ when Folders.Any(f => f.Equals(segments[0], StringComparison.OrdinalIgnoreCase)) => true,
            3 => segments[0].Equals("samples", StringComparison.OrdinalIgnoreCase)
                 && segments[2].Equals("README.md", StringComparison.OrdinalIgnoreCase),
            _ => false,
        };
        return ok ? relative : null;
    }

    /// <summary>The full path of an allowed document that exists; throws for any other path.</summary>
    public string FullPath(string path)
    {
        var relative = Allowed(path) ?? throw new UnauthorizedAccessException(
            $"'{path}' is not a readable document. Readable: README.md, PROJECT.md, docs/**, samples/*/README.md, "
            + "tickets/**, as .md, .txt or .json, given relative to the repository root (see searchDocs).");
        var full = Path.GetFullPath(Path.Combine(Root, relative));
        if (!full.StartsWith(Root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new UnauthorizedAccessException($"'{path}' resolves outside the repository.");
        return File.Exists(full) ? full : throw new FileNotFoundException($"No document '{relative}' (see searchDocs).");
    }

    /// <summary>Every allowed document that exists, as relative paths in ordinal order.</summary>
    public IReadOnlyList<string> Files()
        => RootFiles.Select(f => Path.Combine(Root, f))
            .Concat(Folders.Select(f => Path.Combine(Root, f)).Where(Directory.Exists)
                .SelectMany(d => Directory.EnumerateFiles(d, "*", SearchOption.AllDirectories)))
            .Concat(Directory.Exists(Path.Combine(Root, "samples"))
                ? Directory.EnumerateDirectories(Path.Combine(Root, "samples")).Select(d => Path.Combine(d, "README.md"))
                : [])
            .Where(File.Exists)
            .Select(f => Path.GetRelativePath(Root, f).Replace('\\', '/'))
            .Where(f => Allowed(f) is not null)
            .OrderBy(f => f, StringComparer.Ordinal)
            .ToList();
}
