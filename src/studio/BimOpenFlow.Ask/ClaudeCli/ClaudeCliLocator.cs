using System.Globalization;

namespace BimOpenFlow.Ask;

/// <summary>Finds the Claude Code command-line executable: an explicit override, then PATH, then
/// the desktop app's own copies. Follows chooseClaudeCli in scripts/claude-login.mjs, so the
/// host and that script pick the same file. Every filesystem check and the run check are
/// injectable so tests need no real files or processes.</summary>
public static class ClaudeCliLocator
{
    public const string Variable = "ASK_CLAUDE_CLI";
    public static readonly IReadOnlyList<string> Names = ["claude.exe", "claude.cmd"];

    /// <summary>ASK_CLAUDE_CLI (throws FileNotFoundException when it names a missing file); else
    /// the first Names match on PATH, when runs says it starts; else the numerically newest
    /// claude.exe across %LOCALAPPDATA%\Packages\Claude_*\LocalCache\Roaming\Claude\claude-code
    /// (the MSIX-packaged desktop app's real folder) and %APPDATA%\Claude\claude-code, the
    /// packaged copy winning a tie; else null. runs defaults to ClaudeCliProbe.Runs, which
    /// spawns the PATH hit with --version.</summary>
    public static string? Find(Func<string, string?> environment, Func<string, bool>? fileExists = null,
        Func<string, IReadOnlyList<string>>? subdirectories = null, Func<string, bool>? runs = null)
    {
        fileExists ??= File.Exists;
        subdirectories ??= dir => Directory.Exists(dir) ? Directory.GetDirectories(dir) : [];
        runs ??= ClaudeCliProbe.Runs;

        var overridePath = environment(Variable);
        if (!string.IsNullOrWhiteSpace(overridePath))
        {
            if (!fileExists(overridePath))
                throw new FileNotFoundException($"{Variable} points to a missing file: {overridePath}", overridePath);
            return overridePath;
        }

        // Only the first PATH hit is tried, as in claude-login.mjs: when it does not run it is the
        // virtualized-launcher case, and the bundled copies below are the ones it meant.
        var onPath = FirstOnPath(environment("PATH"), fileExists);
        if (onPath is not null && runs(onPath))
            return onPath;

        return BundledRoots(environment, subdirectories)
            .SelectMany(subdirectories)
            .Select(dir => (Exe: Path.Combine(dir, "claude.exe"), Version: ParseVersion(Path.GetFileName(dir))))
            .Where(entry => entry.Version is not null && fileExists(entry.Exe))
            .OrderByDescending(entry => entry.Version, Comparer<int[]?>.Create(CompareVersions))
            .Select(entry => entry.Exe)
            .FirstOrDefault();
    }

    private static string? FirstOnPath(string? path, Func<string, bool> fileExists)
        => string.IsNullOrWhiteSpace(path)
            ? null
            : path.Split(Path.PathSeparator)
                .Where(dir => !string.IsNullOrWhiteSpace(dir))
                .SelectMany(dir => Names.Select(name => Path.Combine(dir, name)))
                .FirstOrDefault(fileExists);

    /// <summary>Every claude-code folder that holds version subfolders, packaged ones first.</summary>
    private static IReadOnlyList<string> BundledRoots(Func<string, string?> environment,
        Func<string, IReadOnlyList<string>> subdirectories)
    {
        var roots = new List<string>();
        var localAppData = environment("LOCALAPPDATA");
        if (!string.IsNullOrWhiteSpace(localAppData))
            roots.AddRange(subdirectories(Path.Combine(localAppData, "Packages"))
                .Where(dir => Path.GetFileName(dir).StartsWith("Claude_", StringComparison.OrdinalIgnoreCase))
                .Select(dir => Path.Combine(dir, "LocalCache", "Roaming", "Claude", "claude-code")));
        var appData = environment("APPDATA");
        if (!string.IsNullOrWhiteSpace(appData))
            roots.Add(Path.Combine(appData, "Claude", "claude-code"));
        return roots;
    }

    /// <summary>"2.1.281" as [2, 1, 281]; null unless every dot-separated part is a whole number.</summary>
    private static int[]? ParseVersion(string? name)
    {
        if (string.IsNullOrEmpty(name))
            return null;
        var parts = name.Split('.');
        var numbers = new int[parts.Length];
        for (var i = 0; i < parts.Length; i++)
            if (!int.TryParse(parts[i], NumberStyles.None, CultureInfo.InvariantCulture, out numbers[i]))
                return null;
        return numbers;
    }

    /// <summary>Part by part, the shorter version padded with zeros, as compareVersions in claude-login.mjs.</summary>
    private static int CompareVersions(int[]? a, int[]? b)
    {
        a ??= [];
        b ??= [];
        for (var i = 0; i < Math.Max(a.Length, b.Length); i++)
        {
            var diff = (i < a.Length ? a[i] : 0).CompareTo(i < b.Length ? b[i] : 0);
            if (diff != 0)
                return diff;
        }
        return 0;
    }
}
