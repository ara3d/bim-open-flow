using BimOpenFlow.Ask;

namespace BimOpenFlow.Studio.Tests;

/// <summary>Exercises ClaudeCliLocator.Find with fake environment, fileExists, and
/// subdirectories functions, so no real filesystem is needed.</summary>
public sealed class ClaudeCliLocatorTests
{
    private static Func<string, string?> Env(params (string Name, string Value)[] entries)
        => name => entries.FirstOrDefault(e => e.Name == name).Value;

    [Test]
    public void AskClaudeCliWinsOverPath()
    {
        var env = Env(("ASK_CLAUDE_CLI", @"C:\override\claude.exe"), ("PATH", @"C:\a"));
        var found = ClaudeCliLocator.Find(env, fileExists: _ => true);
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
        var found = ClaudeCliLocator.Find(env, fileExists: path => path == @"C:\a\claude.cmd");
        Assert.That(found, Is.EqualTo(@"C:\a\claude.cmd"));
    }

    [Test]
    public void ClaudeExeBeatsClaudeCmdInOneDirectory()
    {
        var env = Env(("PATH", @"C:\a"));
        var found = ClaudeCliLocator.Find(env,
            fileExists: path => path is @"C:\a\claude.exe" or @"C:\a\claude.cmd");
        Assert.That(found, Is.EqualTo(@"C:\a\claude.exe"));
    }

    [Test]
    public void EarlierPathDirectoryWins()
    {
        var env = Env(("PATH", @"C:\a;C:\b"));
        var found = ClaudeCliLocator.Find(env,
            fileExists: path => path is @"C:\a\claude.cmd" or @"C:\b\claude.cmd");
        Assert.That(found, Is.EqualTo(@"C:\a\claude.cmd"));
    }

    [Test]
    public void AppDataPicksHighestVersion()
    {
        var env = Env(("APPDATA", @"C:\R"));
        var root = @"C:\R\Claude\claude-code";
        var found = ClaudeCliLocator.Find(env,
            fileExists: path => path == $@"{root}\2.1.281\claude.exe",
            subdirectories: dir => dir == root
                ? [$@"{root}\2.1.280", $@"{root}\2.1.281"]
                : []);
        Assert.That(found, Is.EqualTo($@"{root}\2.1.281\claude.exe"));
    }

    [Test]
    public void AppDataOrdersVersionsNumerically()
    {
        var env = Env(("APPDATA", @"C:\R"));
        var root = @"C:\R\Claude\claude-code";
        var found = ClaudeCliLocator.Find(env,
            fileExists: path => path == $@"{root}\2.10.0\claude.exe",
            subdirectories: dir => dir == root
                ? [$@"{root}\2.9.9", $@"{root}\2.10.0"]
                : []);
        Assert.That(found, Is.EqualTo($@"{root}\2.10.0\claude.exe"));
    }

    [Test]
    public void NothingFoundGivesNull()
    {
        var env = Env();
        var found = ClaudeCliLocator.Find(env, fileExists: _ => false, subdirectories: _ => []);
        Assert.That(found, Is.Null);
    }
}
