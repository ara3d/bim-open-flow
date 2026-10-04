using System.Text.Json.Nodes;

namespace BimOpenFlow.Ask;

/// <summary>One claude -p session, reached one process per SendAsync. The first call starts a
/// fresh session; every later call passes --resume with the session id the CLI reported, so a
/// follow-up or a host check round continues the same conversation.</summary>
internal sealed class ClaudeCliConversation(
    AskSetup setup, ClaudeCliSettings settings, string systemPromptFile, string mcpConfigFile)
    : IAskConversation
{
    private string? _sessionId;

    public async Task<AskOutcome> SendAsync(string user, Func<AskEvent, Task> emit, CancellationToken ct)
    {
        var arguments = ClaudeCliArguments.Build(settings, setup, systemPromptFile, mcpConfigFile, _sessionId);
        var stream = new ClaudeStream(setup.ServerKey);
        var sawInit = false;

        var (exitCode, stderrTail) = await ClaudeCliProcess.RunAsync(
            settings.Executable, arguments, settings.WorkDirectory, user,
            async line =>
            {
                var events = stream.Read(line);
                if (!sawInit && stream.Servers is not null)
                {
                    sawInit = true;
                    ValidateInit(line, stream.Servers);
                }
                foreach (var e in events)
                    await emit(e);
            },
            settings.Timeout, ct);

        if (stream.SessionId is not null)
            _sessionId = stream.SessionId;

        var result = stream.Result;
        if (result is null)
        {
            var tail = stderrTail.Length <= 400 ? stderrTail : stderrTail[^400..];
            throw new ClaudeCliException(
                $"Claude Code exited with code {exitCode}: {(string.IsNullOrWhiteSpace(tail) ? "no output" : tail.Trim())}");
        }

        if (result.IsError)
        {
            throw result.Subtype == "error_max_turns"
                ? new ClaudeCliException(AskAgent.TurnLimitMessage(setup.MaxTurns))
                : new ClaudeCliException($"Claude Code: {(string.IsNullOrWhiteSpace(result.Text) ? result.Subtype : result.Text)}");
        }

        return new AskOutcome(result.Text, result.Turns, result.InputTokens, result.OutputTokens);
    }

    /// <summary>An init line is only good when our server connected and the tool list it offers
    /// is not empty; either failure means the model would have nothing to call, so the process is
    /// stopped before it wastes a turn.</summary>
    private void ValidateInit(string line, IReadOnlyList<McpServerStatus> servers)
    {
        var status = servers.FirstOrDefault(s => s.Name == setup.ServerKey)?.Status;
        var toolsEmpty = InitToolsAreEmpty(line);
        if (status != "connected" || toolsEmpty)
            throw new ClaudeCliException(
                $"Claude Code could not connect to the {setup.ServerKey} tool server at {setup.Tools.Url} (status {status ?? "missing"}).");
    }

    private static bool InitToolsAreEmpty(string line)
    {
        JsonObject? json;
        try
        {
            json = JsonNode.Parse(line) as JsonObject;
        }
        catch (System.Text.Json.JsonException)
        {
            return false;
        }
        return (json?["tools"] as JsonArray)?.Count is 0;
    }
}
