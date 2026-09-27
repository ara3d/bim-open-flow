namespace BimOpenFlow.Ask;

/// <summary>Builds the fixed claude command line, its MCP config file, and the child environment
/// stripped of the variables that would let it reach a retired account or refuse to start nested
/// inside a Claude Code session.</summary>
public static class ClaudeCliArguments
{
    /// <summary>The one command line every call passes, pinned by the plan: no built-in tools, no
    /// settings, only setup.ServerKey's tools, and --resume when resumeSessionId is given.</summary>
    public static IReadOnlyList<string> Build(ClaudeCliSettings settings, AskSetup setup,
        string systemPromptFile, string mcpConfigFile, string? resumeSessionId)
    {
        var arguments = new List<string>
        {
            "-p",
            "--output-format", "stream-json",
            "--verbose",
            "--model", settings.Model,
            "--effort", settings.Effort,
            "--system-prompt-file", systemPromptFile,
            "--mcp-config", mcpConfigFile,
            "--strict-mcp-config",
            "--setting-sources", "",
            "--tools", "",
            "--allowedTools", $"mcp__{setup.ServerKey}",
        };

        if (setup.Hidden.Count > 0)
        {
            var hidden = setup.Hidden
                .OrderBy(name => name, StringComparer.Ordinal)
                .Select(name => $"mcp__{setup.ServerKey}__{name}");
            arguments.Add("--disallowedTools");
            arguments.Add(string.Join(",", hidden));
        }

        arguments.Add("--permission-mode");
        arguments.Add("dontAsk");
        arguments.Add("--disable-slash-commands");
        arguments.Add("--max-turns");
        arguments.Add(setup.MaxTurns.ToString());

        if (resumeSessionId is not null)
        {
            arguments.Add("--resume");
            arguments.Add(resumeSessionId);
        }

        return arguments;
    }

    /// <summary>The --mcp-config file content: one HTTP server named serverKey at url.</summary>
    public static string McpConfig(string serverKey, string url)
        => "{\"mcpServers\":{\"" + serverKey + "\":{\"type\":\"http\",\"url\":\"" + url + "\"}}}";

    /// <summary>The CLAUDE_* names a spawned claude may still see, because they carry login and
    /// configuration state rather than routing to an account or the parent session's identity.</summary>
    public static readonly IReadOnlySet<string> KeptVariables = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "CLAUDE_CONFIG_DIR",
        "CLAUDE_CODE_OAUTH_TOKEN",
        "CLAUDE_CODE_GIT_BASH_PATH",
    };

    /// <summary>Removes ANTHROPIC_* and CLAUDECODE / CLAUDE_* (except KeptVariables) by name;
    /// never reads a value, since Windows environment names are case-insensitive.</summary>
    public static void PrepareEnvironment(IDictionary<string, string?> environment)
    {
        var toRemove = environment.Keys
            .Where(name =>
                name.StartsWith("ANTHROPIC_", StringComparison.OrdinalIgnoreCase) ||
                ((name.Equals("CLAUDECODE", StringComparison.OrdinalIgnoreCase) ||
                  name.StartsWith("CLAUDE_", StringComparison.OrdinalIgnoreCase)) &&
                 !KeptVariables.Contains(name)))
            .ToList();

        foreach (var name in toRemove)
            environment.Remove(name);
    }
}
