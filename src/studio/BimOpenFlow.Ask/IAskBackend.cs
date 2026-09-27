using Ara3D.MCP;

namespace BimOpenFlow.Ask;

/// <summary>Starts conversations with an agent that answers by calling one MCP server's tools.
/// ChatBackend runs the tool loop in process over an IChatModel; ClaudeCliBackend hands it to
/// the Claude Code command line.</summary>
public interface IAskBackend
{
    IAskConversation Start(string system);
}

/// <summary>One conversation. Each send adds a user message and lets the agent work until it
/// answers, reporting tool calls and interim text through emit; later sends see everything
/// earlier ones did. Throws with one sentence a person can act on when no answer comes.</summary>
public interface IAskConversation
{
    Task<AskOutcome> SendAsync(string user, Func<AskEvent, Task> emit, CancellationToken ct);
}

/// <summary>The tool server a backend drives, the name the model knows it by (Claude Code
/// prefixes tools with mcp__{ServerKey}__), the tools not offered, and the model-turn limit
/// per message.</summary>
public sealed record AskSetup(McpServer Tools, string ServerKey, IReadOnlySet<string> Hidden, int MaxTurns);
