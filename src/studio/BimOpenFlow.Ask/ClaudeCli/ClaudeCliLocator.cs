namespace BimOpenFlow.Ask;

/// <summary>Finds the Claude Code command-line executable without spawning anything: an
/// explicit override, then PATH, then the desktop app's own copy. Every filesystem check is
/// injectable so tests need no real files.</summary>
public static class ClaudeCliLocator
{
    public const string Variable = "ASK_CLAUDE_CLI";
    public static readonly IReadOnlyList<string> Names = ["claude.exe", "claude.cmd"];

    /// <summary>ASK_CLAUDE_CLI (throws FileNotFoundException when it names a missing file); else
    /// the first Names match in each PATH directory in order; else the highest-Version
    /// %APPDATA%\Claude\claude-code\&lt;version&gt;\claude.exe; else null.</summary>
    public static string? Find(Func<string, string?> environment, Func<string, bool>? fileExists = null,
        Func<string, IEnumerable<string>>? subdirectories = null)
    {
        fileExists ??= File.Exists;
        subdirectories ??= dir => Directory.Exists(dir) ? Directory.GetDirectories(dir) : [];

        var overridePath = environment(Variable);
        if (!string.IsNullOrWhiteSpace(overridePath))
        {
            if (!fileExists(overridePath))
                throw new FileNotFoundException($"{Variable} points to a missing file: {overridePath}", overridePath);
            return overridePath;
        }

        var path = environment("PATH");
        if (!string.IsNullOrWhiteSpace(path))
        {
            foreach (var dir in path.Split(Path.PathSeparator))
            {
                if (string.IsNullOrWhiteSpace(dir))
                    continue;
                foreach (var name in Names)
                {
                    var candidate = Path.Combine(dir, name);
                    if (fileExists(candidate))
                        return candidate;
                }
            }
        }

        var appData = environment("APPDATA");
        if (string.IsNullOrWhiteSpace(appData))
            return null;

        var claudeCodeRoot = Path.Combine(appData, "Claude", "claude-code");
        var best = subdirectories(claudeCodeRoot)
            .Select(dir => (Dir: dir, Version: ParseVersion(Path.GetFileName(dir))))
            .Where(entry => entry.Version is not null)
            .OrderByDescending(entry => entry.Version)
            .Select(entry => entry.Dir)
            .FirstOrDefault();

        if (best is null)
            return null;

        var exe = Path.Combine(best, "claude.exe");
        return fileExists(exe) ? exe : null;
    }

    private static Version? ParseVersion(string? name) =>
        Version.TryParse(name, out var version) ? version : null;
}
