namespace BimOpenFlow.Host.Catalog;

/// <summary>A file found under a model root. Paths use forward slashes.</summary>
public sealed record RootFile(string Path, string Relative, long SizeBytes);

/// <summary>One entry of a folder listing inside a model root.</summary>
public sealed record RootEntry(string Name, string Path, bool IsDirectory, long? SizeBytes);

/// <summary>A folder of a model root. <see cref="Dir"/> is empty for the list of roots itself;
/// <see cref="Parent"/> is null there, and empty for a root (its parent is that list).</summary>
public sealed record RootFolder(string Dir, string? Parent, IReadOnlyList<RootEntry> Entries);

/// <summary>Where a path sits relative to the model roots. <see cref="Exists"/> is true when the file is
/// there (a glob in the last segment counts when it matches), wherever the path points;
/// <see cref="Root"/> and <see cref="Relative"/> are set only when it lies under a root.</summary>
public sealed record RootPathStatus(string Path, bool Exists, string? Root, string? Relative);

/// <summary>
/// Lists, browses, and locates files under the host's model roots, for the editor's FilePath
/// control. Browsing never leaves the roots; existence checks answer for any path, because a run
/// reads the path as written.
/// </summary>
public sealed class ModelRootFiles
{
    /// <summary>Most files one listing returns; deeper corpora get a narrower extension list or a browse.</summary>
    public const int MaxFiles = 1000;

    private static readonly StringComparison PathComparison =
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    private static readonly EnumerationOptions Recursive =
        new() { RecurseSubdirectories = true, IgnoreInaccessible = true };

    private static readonly EnumerationOptions Flat = new() { IgnoreInaccessible = true };

    public IReadOnlyList<string> Roots { get; }

    public ModelRootFiles(IEnumerable<string> roots)
        => Roots = roots.Select(r => Slash(Path.GetFullPath(r)).TrimEnd('/')).ToList();

    /// <summary>Files under any root whose extension is in <paramref name="extensions"/> (a
    /// comma-separated list such as ".ifc,.bos"; empty accepts every file), ordered by root then path.
    /// At most <see cref="MaxFiles"/>; <paramref name="truncated"/> says whether more existed.</summary>
    public IReadOnlyList<RootFile> List(string extensions, out bool truncated)
    {
        var wanted = ParseExtensions(extensions);
        var found = new List<RootFile>();
        truncated = false;
        foreach (var root in Roots.Where(Directory.Exists))
        {
            var files = Directory.EnumerateFiles(root, "*", Recursive)
                .Select(Slash)
                .Where(f => Accepts(wanted, f))
                .OrderBy(f => f, StringComparer.Ordinal);
            foreach (var file in files)
            {
                if (found.Count == MaxFiles)
                {
                    truncated = true;
                    return found;
                }
                found.Add(new(file, file[(root.Length + 1)..], new FileInfo(file).Length));
            }
        }
        return found;
    }

    /// <summary>The folders and files directly inside <paramref name="dir"/>, folders first. An empty
    /// <paramref name="dir"/> lists the roots. Throws <see cref="ArgumentException"/> for a folder
    /// outside the roots and <see cref="DirectoryNotFoundException"/> for one that is missing.</summary>
    public RootFolder Browse(string dir, string extensions)
    {
        if (string.IsNullOrWhiteSpace(dir))
            return new("", null, Roots.Where(Directory.Exists)
                .Select(r => new RootEntry(r, r, true, null)).ToList());
        var full = Slash(Path.GetFullPath(dir)).TrimEnd('/');
        var root = RootOf(full) ?? throw new ArgumentException($"'{dir}' is not under a model root.");
        if (!Directory.Exists(full))
            throw new DirectoryNotFoundException($"Folder not found: {full}");
        var wanted = ParseExtensions(extensions);
        var folders = Directory.EnumerateDirectories(full, "*", Flat).Select(Slash)
            .OrderBy(d => d, StringComparer.OrdinalIgnoreCase)
            .Select(d => new RootEntry(Path.GetFileName(d), d, true, null));
        var files = Directory.EnumerateFiles(full, "*", Flat).Select(Slash)
            .Where(f => Accepts(wanted, f))
            .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
            .Select(f => new RootEntry(Path.GetFileName(f), f, false, new FileInfo(f).Length));
        var parent = string.Equals(full, root, PathComparison) ? "" : Slash(Path.GetDirectoryName(full)!);
        return new(full, parent, folders.Concat(files).ToList());
    }

    /// <summary>Whether <paramref name="path"/> exists and which root it sits under.</summary>
    public RootPathStatus Stat(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return new(path, false, null, null);
        var full = Slash(Path.GetFullPath(path));
        var root = RootOf(full);
        return new(path, Exists(full), root, root is null ? null : full[(root.Length + 1)..]);
    }

    private static bool Exists(string full)
    {
        var name = Path.GetFileName(full);
        if (name.IndexOfAny(['*', '?']) < 0)
            return File.Exists(full);
        var dir = Path.GetDirectoryName(full);
        return dir is not null && Directory.Exists(dir)
            && Directory.EnumerateFiles(dir, name, Flat).Any();
    }

    private string? RootOf(string full)
        => Roots.FirstOrDefault(r => full.StartsWith(r + "/", PathComparison)
            || string.Equals(full, r, PathComparison));

    private static HashSet<string> ParseExtensions(string extensions)
        => extensions.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(e => e.StartsWith('.') ? e : "." + e)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

    private static bool Accepts(HashSet<string> wanted, string file)
        => wanted.Count == 0 || wanted.Contains(Path.GetExtension(file));

    private static string Slash(string path) => path.Replace('\\', '/');
}
