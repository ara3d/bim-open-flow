using System.Security.Cryptography;
using System.Text;
using Ara3D.MCP;

namespace BimOpenFlow.Ask;

/// <summary>Hands the whole tool loop to the Claude Code command line: setup.Tools serves the
/// model's calls over HTTP, and every SendAsync on the conversation it returns runs one claude -p
/// process against that server.</summary>
public sealed class ClaudeCliBackend : IAskBackend
{
    public const string ProviderName = "claude-cli";

    private readonly AskSetup _setup;
    private readonly ClaudeCliSettings _settings;
    private readonly string _mcpConfigFile;

    /// <summary>Starts setup.Tools over HTTP if it is not listening (ArgumentException when its
    /// transport is not HTTP; ClaudeCliException when the listener cannot start), creates the
    /// work directory, and writes mcp-{key}-{port}.json there.</summary>
    public ClaudeCliBackend(AskSetup setup, ClaudeCliSettings settings)
    {
        _setup = setup;
        _settings = settings;

        if (!setup.Tools.Active)
        {
            if (setup.Tools.Transport != McpTransport.Http)
                throw new ArgumentException(
                    "ClaudeCliBackend needs setup.Tools built with McpTransport.Http.", nameof(setup));
            try
            {
                setup.Tools.Start();
            }
            catch (Exception e)
            {
                throw new ClaudeCliException($"Could not start the {setup.ServerKey} tool server: {e.Message}");
            }
        }

        Directory.CreateDirectory(settings.WorkDirectory);
        var url = setup.Tools.Url ?? throw new ClaudeCliException($"The {setup.ServerKey} tool server has no URL after starting.");
        var port = new Uri(url).Port;
        _mcpConfigFile = Path.Combine(settings.WorkDirectory, $"mcp-{setup.ServerKey}-{port}.json");
        File.WriteAllText(_mcpConfigFile, ClaudeCliArguments.McpConfig(setup.ServerKey, url));
    }

    /// <summary>Writes system-{first 16 hex of SHA-256}.md to the work directory; the conversation
    /// has no session until its first send.</summary>
    public IAskConversation Start(string system)
    {
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(system)))[..16].ToLowerInvariant();
        var systemPromptFile = Path.Combine(_settings.WorkDirectory, $"system-{hash}.md");
        File.WriteAllText(systemPromptFile, system);
        return new ClaudeCliConversation(_setup, _settings, systemPromptFile, _mcpConfigFile);
    }
}
