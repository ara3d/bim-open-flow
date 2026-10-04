using Ara3D.MCP;
using BimOpenFlow.Ask;

namespace BimOpenFlow.Ask.Tests;

/// <summary>Checks ClaudeCliArguments.Build against the plan's worked example character for
/// character, McpConfig's JSON, and that PrepareEnvironment removes exactly the named variables
/// from a plain dictionary, never a real process environment.</summary>
public sealed class ClaudeCliArgumentsTests
{
    private static AskSetup Setup(IReadOnlySet<string> hidden) =>
        new(new McpServer(0, "test", "0"), "bimopenflow", hidden, 60);

    [Test]
    public void BuildMatchesTheWorkedExample()
    {
        var settings = new ClaudeCliSettings("claude.cmd", "claude-haiku-4-5-20251001", "medium", @"C:\T");
        var setup = Setup(new HashSet<string> { "listDatabases", "getNodeCatalog" });

        var arguments = ClaudeCliArguments.Build(settings, setup,
            @"C:\T\system-ab12cd34.md", @"C:\T\mcp-bimopenflow-50123.json", null);

        Assert.That(arguments, Is.EqualTo(new[]
        {
            "-p", "--output-format", "stream-json", "--verbose", "--model", "claude-haiku-4-5-20251001",
            "--effort", "medium",
            "--system-prompt-file", @"C:\T\system-ab12cd34.md",
            "--mcp-config", @"C:\T\mcp-bimopenflow-50123.json",
            "--strict-mcp-config",
            "--setting-sources", "",
            "--tools", "",
            "--allowedTools", "mcp__bimopenflow",
            "--disallowedTools", "mcp__bimopenflow__getNodeCatalog,mcp__bimopenflow__listDatabases",
            "--permission-mode", "dontAsk",
            "--disable-slash-commands",
            "--max-turns", "60",
        }));
    }

    [Test]
    public void ResumeAppendsResumeAndSessionId()
    {
        var settings = new ClaudeCliSettings("claude.cmd", "claude-haiku-4-5-20251001", "medium", @"C:\T");
        var setup = Setup(new HashSet<string> { "listDatabases", "getNodeCatalog" });

        var arguments = ClaudeCliArguments.Build(settings, setup,
            @"C:\T\system-ab12cd34.md", @"C:\T\mcp-bimopenflow-50123.json", "s1");

        Assert.That(arguments[^2], Is.EqualTo("--resume"));
        Assert.That(arguments[^1], Is.EqualTo("s1"));
    }

    [Test]
    public void EmptyHiddenLeavesOutDisallowedTools()
    {
        var settings = new ClaudeCliSettings("claude.cmd", "claude-haiku-4-5-20251001", "medium", @"C:\T");
        var setup = Setup(new HashSet<string>());

        var arguments = ClaudeCliArguments.Build(settings, setup,
            @"C:\T\system-ab12cd34.md", @"C:\T\mcp-bimopenflow-50123.json", null);

        Assert.That(arguments, Has.None.EqualTo("--disallowedTools"));
    }

    [Test]
    public void McpConfigMatchesTheExample()
    {
        var json = ClaudeCliArguments.McpConfig("bimopenflow", "http://127.0.0.1:50123/mcp");
        Assert.That(json, Is.EqualTo(
            """{"mcpServers":{"bimopenflow":{"type":"http","url":"http://127.0.0.1:50123/mcp"}}}"""));
    }

    private static Dictionary<string, string?> SampleEnvironment() => new(StringComparer.OrdinalIgnoreCase)
    {
        ["ANTHROPIC_API_KEY"] = "sk-dummy",
        ["ANTHROPIC_BASE_URL"] = "https://example.invalid",
        ["CLAUDECODE"] = "1",
        ["CLAUDE_EFFORT"] = "high",
        ["CLAUDE_CODE_ENTRYPOINT"] = "cli",
        ["CLAUDE_CONFIG_DIR"] = @"C:\dummy\config",
        ["CLAUDE_CODE_OAUTH_TOKEN"] = "dummy-token",
        ["CLAUDE_CODE_GIT_BASH_PATH"] = @"C:\dummy\bash.exe",
        ["PATH"] = @"C:\a;C:\b",
        ["APPDATA"] = @"C:\dummy\AppData",
    };

    [Test]
    public void PrepareEnvironmentRemovesAnthropicAndClaudeVariablesExceptTheKeptOnes()
    {
        var env = SampleEnvironment();

        ClaudeCliArguments.PrepareEnvironment(env);

        Assert.That(env.Keys, Does.Not.Contain("ANTHROPIC_API_KEY"));
        Assert.That(env.Keys, Does.Not.Contain("ANTHROPIC_BASE_URL"));
        Assert.That(env.Keys, Does.Not.Contain("CLAUDECODE"));
        Assert.That(env.Keys, Does.Not.Contain("CLAUDE_EFFORT"));
        Assert.That(env.Keys, Does.Not.Contain("CLAUDE_CODE_ENTRYPOINT"));

        Assert.That(env, Contains.Key("CLAUDE_CONFIG_DIR"));
        Assert.That(env, Contains.Key("CLAUDE_CODE_OAUTH_TOKEN"));
        Assert.That(env, Contains.Key("CLAUDE_CODE_GIT_BASH_PATH"));
        Assert.That(env, Contains.Key("PATH"));
        Assert.That(env, Contains.Key("APPDATA"));
    }

    [Test]
    public void PrepareEnvironmentIsCaseInsensitiveAndNeverReadsAValue()
    {
        var env = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["anthropic_api_key"] = null,
            ["claudecode"] = null,
            ["PATH"] = @"C:\a",
        };

        ClaudeCliArguments.PrepareEnvironment(env);

        Assert.That(env.Keys, Does.Not.Contain("anthropic_api_key"));
        Assert.That(env.Keys, Does.Not.Contain("claudecode"));
        Assert.That(env, Contains.Key("PATH"));
    }
}
