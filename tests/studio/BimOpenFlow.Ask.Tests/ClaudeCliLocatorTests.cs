using BimOpenFlow.Ask;

namespace BimOpenFlow.Ask.Tests;

/// <summary>Exercises ClaudeCliLocator.Find with fake environment, fileExists, subdirectories, and
/// runs functions, so no real filesystem or process is needed.</summary>
public sealed class ClaudeCliLocatorTests
{
    private const string Packages = @"C:\L\Packages";
    private const string Packaged = @"C:\L\Packages\Claude_abc\LocalCache\Roaming\Claude\claude-code";
    private const string AppData = @"C:\R\Claude\claude-code";

    private static Func<string, string?> Env(params (string Name, string Value)[] entries)
        => name => entries.FirstOrDefault(e => e.Name == name).Value;

    /// <summary>A fake tree: the Packages folder lists Claude_abc and an unrelated package, and each
    /// claude-code root lists the given version folders, every one holding a claude.exe.</summary>
    private static string? FindBundled(string[] packagedVersions, string[] appDataVersions)
    {
        var existing = packagedVersions.Select(v => $@"{Packaged}\{v}\claude.exe")
            .Concat(appDataVersions.Select(v => $@"{AppData}\{v}\claude.exe"))
            .ToHashSet();
        return ClaudeCliLocator.Find(Env(("LOCALAPPDATA", @"C:\L"), ("APPDATA", @"C:\R")),
            fileExists: existing.Contains,
            subdirectories: dir => dir switch
            {
                Packages => [$@"{Packages}\Claude_abc", $@"{Packages}\Other_xyz"],
                Packaged => packagedVersions.Select(v => $@"{Packaged}\{v}").ToArray(),
                AppData => appDataVersions.Select(v => $@"{AppData}\{v}").ToArray(),
                _ => [],
            },
            runs: _ => true);
    }

    [Test]
    public void AskClaudeCliWinsOverPath()
    {
        var env = Env(("ASK_CLAUDE_CLI", @"C:\override\claude.exe"), ("PATH", @"C:\a"));
        var found = ClaudeCliLocator.Find(env, fileExists: _ => true,
            runs: _ => throw new AssertionException("the override is trusted without running it"));
        Assert.That(found, Is.EqualTo(@"C:\override\claude.exe"));
    }

    [Test]
    public void MissingAskClaudeCliThrows()
    {
        var env = Env(("ASK_CLAUDE_CLI", @"C:\override\claude.exe"));
        var ex = Assert.Throws<FileNotFoundException>(() => ClaudeCliLocator.Find(env, fileExists: _ => false));
        Assert.That(ex!.Message, Does.Contain(ClaudeCliLocator.Variable));
    }

    [Test]
    public void ClaudeCmdOnPathIsFound()
    {
        var env = Env(("PATH", @"C:\a"));
        var found = ClaudeCliLocator.Find(env, fileExists: path => path == @"C:\a\claude.cmd", runs: _ => true);
        Assert.That(found, Is.EqualTo(@"C:\a\claude.cmd"));
    }

    [Test]
    public void ClaudeExeBeatsClaudeCmdInOneDirectory()
    {
        var env = Env(("PATH", @"C:\a"));
        var found = ClaudeCliLocator.Find(env,
            fileExists: path => path is @"C:\a\claude.exe" or @"C:\a\claude.cmd", runs: _ => true);
        Assert.That(found, Is.EqualTo(@"C:\a\claude.exe"));
    }

    [Test]
    public void EarlierPathDirectoryWins()
    {
        var env = Env(("PATH", @"C:\a;C:\b"));
        var found = ClaudeCliLocator.Find(env,
            fileExists: path => path is @"C:\a\claude.cmd" or @"C:\b\claude.cmd", runs: _ => true);
        Assert.That(found, Is.EqualTo(@"C:\a\claude.cmd"));
    }

    [Test]
    public void APathHitThatRunsBeatsABundledCopy()
    {
        var found = ClaudeCliLocator.Find(Env(("PATH", @"C:\a"), ("APPDATA", @"C:\R")),
            fileExists: _ => true,
            subdirectories: dir => dir == AppData ? [$@"{AppData}\9.0.0"] : [],
            runs: path => path == @"C:\a\claude.exe");
        Assert.That(found, Is.EqualTo(@"C:\a\claude.exe"));
    }

    [Test]
    public void APathHitThatDoesNotRunFallsThroughToTheBundledCopy()
    {
        var tried = new List<string>();
        var found = ClaudeCliLocator.Find(Env(("PATH", @"C:\a;C:\b"), ("APPDATA", @"C:\R")),
            fileExists: path => path is @"C:\a\claude.cmd" or @"C:\b\claude.exe" || path.StartsWith(AppData),
            subdirectories: dir => dir == AppData ? [$@"{AppData}\2.1.281"] : [],
            runs: path =>
            {
                tried.Add(path);
                return false;
            });
        Assert.That(found, Is.EqualTo($@"{AppData}\2.1.281\claude.exe"));
        Assert.That(tried, Is.EqualTo(new[] { @"C:\a\claude.cmd" }));
    }

    [Test]
    public void APathHitThatDoesNotRunAndNoBundledCopyGivesNull()
    {
        var found = ClaudeCliLocator.Find(Env(("PATH", @"C:\a")),
            fileExists: _ => true, subdirectories: _ => [], runs: _ => false);
        Assert.That(found, Is.Null);
    }

    [Test]
    public void AppDataPicksHighestVersion()
        => Assert.That(FindBundled([], ["2.1.280", "2.1.281"]), Is.EqualTo($@"{AppData}\2.1.281\claude.exe"));

    [Test]
    public void PackagedCopyIsFound()
        => Assert.That(FindBundled(["2.1.281"], []), Is.EqualTo($@"{Packaged}\2.1.281\claude.exe"));

    [Test]
    public void OnlyClaudePackagesAreSearched()
    {
        var found = ClaudeCliLocator.Find(Env(("LOCALAPPDATA", @"C:\L")),
            fileExists: _ => true,
            subdirectories: dir => dir == Packages ? [$@"{Packages}\Other_xyz"]
                : dir.Contains("Other_xyz") ? [$@"{dir}\9.9.9"]
                : [],
            runs: _ => true);
        Assert.That(found, Is.Null);
    }

    [Test]
    public void NewestWinsAcrossPackagedAndAppData()
    {
        Assert.That(FindBundled(["2.1.280"], ["2.1.281"]), Is.EqualTo($@"{AppData}\2.1.281\claude.exe"));
        Assert.That(FindBundled(["2.10.0"], ["2.9.9"]), Is.EqualTo($@"{Packaged}\2.10.0\claude.exe"));
    }

    [Test]
    public void PackagedWinsATie()
        => Assert.That(FindBundled(["2.1.281"], ["2.1.281"]), Is.EqualTo($@"{Packaged}\2.1.281\claude.exe"));

    [Test]
    public void VersionsAreComparedPartByPartWithZeroPadding()
        => Assert.That(FindBundled(["2.1"], ["2.0.9", "2.1.0.1"]), Is.EqualTo($@"{AppData}\2.1.0.1\claude.exe"));

    [Test]
    public void NonVersionFoldersAreIgnored()
        => Assert.That(FindBundled(["latest", "2.x"], ["1.0.0"]), Is.EqualTo($@"{AppData}\1.0.0\claude.exe"));

    [Test]
    public void AVersionFolderWithoutClaudeExeIsSkipped()
    {
        var found = ClaudeCliLocator.Find(Env(("APPDATA", @"C:\R")),
            fileExists: path => path == $@"{AppData}\2.1.280\claude.exe",
            subdirectories: dir => dir == AppData ? [$@"{AppData}\2.1.280", $@"{AppData}\2.1.281"] : []);
        Assert.That(found, Is.EqualTo($@"{AppData}\2.1.280\claude.exe"));
    }

    [Test]
    public void ClaudeExeOneFolderBelowTheVersionIsFound()
    {
        var found = ClaudeCliLocator.Find(Env(("LOCALAPPDATA", @"C:\L")),
            fileExists: path => path == $@"{Packaged}\2.1.286\635c1867224a\claude.exe",
            subdirectories: dir => dir switch
            {
                Packages => [$@"{Packages}\Claude_abc"],
                Packaged => [$@"{Packaged}\2.1.284", $@"{Packaged}\2.1.286"],
                $@"{Packaged}\2.1.286" => [$@"{Packaged}\2.1.286\635c1867224a"],
                _ => [],
            });
        Assert.That(found, Is.EqualTo($@"{Packaged}\2.1.286\635c1867224a\claude.exe"));
    }

    [Test]
    public void NothingFoundGivesNull()
    {
        var found = ClaudeCliLocator.Find(Env(), fileExists: _ => false, subdirectories: _ => []);
        Assert.That(found, Is.Null);
    }

    /// <summary>Runs the real locator on this machine and prints what it found; asserts nothing,
    /// because a build machine may have no Claude Code at all.</summary>
    [Test, Explicit("depends on this machine")]
    public void FindOnThisMachine()
        => TestContext.Out.WriteLine(
            $"ClaudeCliLocator.Find: {ClaudeCliLocator.Find(Environment.GetEnvironmentVariable) ?? "(null)"}");
}
