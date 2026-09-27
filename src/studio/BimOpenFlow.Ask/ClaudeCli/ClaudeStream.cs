using System.Text.Json.Nodes;

namespace BimOpenFlow.Ask;

/// <summary>The outcome Claude Code's final "result" line carries: whether it failed, its
/// subtype ("success", "error_max_turns", ...), the answer text, the model-turn count, token
/// totals (input includes the cache-read and cache-creation counts, as AnthropicChat counts
/// them), and the session id to resume.</summary>
public sealed record ClaudeResult(bool IsError, string Subtype, string Text, int Turns, long InputTokens,
    long OutputTokens, string? SessionId);

/// <summary>One MCP server's connection state, as the "init" line reports it.</summary>
public sealed record McpServerStatus(string Name, string Status);

/// <summary>Reads Claude Code's `--output-format stream-json` lines into the same AskEvents the
/// in-process loop emits. Pure: it holds only what a later line needs (a pending assistant text
/// block, and the fields callers read after the stream ends) and never touches the network or
/// the filesystem.</summary>
public sealed class ClaudeStream(string serverKey)
{
    private string? _pendingText;
    private readonly Dictionary<string, (string Name, JsonNode? Input)> _pendingCalls = new(StringComparer.Ordinal);

    public string? SessionId { get; private set; }
    public IReadOnlyList<McpServerStatus>? Servers { get; private set; }
    public ClaudeResult? Result { get; private set; }

    /// <summary>The events this line completes (zero or more).</summary>
    public IReadOnlyList<AskEvent> Read(string line)
    {
        JsonObject? json;
        try
        {
            json = JsonNode.Parse(line) as JsonObject;
        }
        catch (System.Text.Json.JsonException)
        {
            return [];
        }
        if (json is null)
            return [];

        var type = json["type"]?.GetValue<string>();
        return type switch
        {
            "system" => ReadSystem(json),
            "assistant" => ReadAssistant(json),
            "user" => ReadUser(json),
            "result" => ReadFinalResult(json),
            _ => [],
        };
    }

    /// <summary>Strips Claude Code's "mcp__{serverKey}__" prefix from a tool name; a name without
    /// that prefix (not one of ours) is returned unchanged.</summary>
    public static string ToolName(string serverKey, string cliName)
    {
        var prefix = $"mcp__{serverKey}__";
        return cliName.StartsWith(prefix, StringComparison.Ordinal) ? cliName[prefix.Length..] : cliName;
    }

    private IReadOnlyList<AskEvent> ReadSystem(JsonObject json)
    {
        if (json["subtype"]?.GetValue<string>() != "init")
            return [];
        SessionId = json["session_id"]?.GetValue<string>();
        Servers = (json["mcp_servers"] as JsonArray)?.OfType<JsonObject>()
            .Select(s => new McpServerStatus(s["name"]?.GetValue<string>() ?? "", s["status"]?.GetValue<string>() ?? ""))
            .ToList() ?? [];
        return [];
    }

    private IReadOnlyList<AskEvent> ReadAssistant(JsonObject json)
    {
        var content = json["message"]?["content"] as JsonArray;
        var events = new List<AskEvent>();
        foreach (var block in content?.OfType<JsonObject>() ?? [])
        {
            var blockType = block["type"]?.GetValue<string>();
            if (blockType == "text")
            {
                // Held until the next tool_use: the last held text is the final answer and is
                // dropped, because result.result carries it (matches AskAgent's text timing).
                _pendingText = block["text"]?.GetValue<string>();
            }
            else if (blockType == "tool_use")
            {
                if (_pendingText is not null)
                {
                    events.Add(new AskEvent("text", Text: _pendingText));
                    _pendingText = null;
                }
                var id = block["id"]?.GetValue<string>() ?? "";
                var name = ToolName(serverKey, block["name"]?.GetValue<string>() ?? "");
                _pendingCalls[id] = (name, block["input"]);
            }
        }
        return events;
    }

    private IReadOnlyList<AskEvent> ReadUser(JsonObject json)
    {
        var content = json["message"]?["content"] as JsonArray;
        var events = new List<AskEvent>();
        foreach (var block in content?.OfType<JsonObject>() ?? [])
        {
            if (block["type"]?.GetValue<string>() != "tool_result")
                continue;
            var id = block["tool_use_id"]?.GetValue<string>() ?? "";
            var (name, args) = _pendingCalls.TryGetValue(id, out var call) ? call : ("", null);
            var text = ToolResultText(block["content"]);
            var isError = block["is_error"]?.GetValue<bool>() ?? false;
            var (ok, _, summary) = AskAgent.ReadResult(name, text);
            events.Add(new AskEvent("tool", name, args, isError ? false : ok, isError ? text : summary));
        }
        return events;
    }

    private static string ToolResultText(JsonNode? content)
    {
        if (content is JsonValue value && value.TryGetValue<string>(out var s))
            return s;
        if (content is JsonArray array)
            return string.Concat(array.OfType<JsonObject>()
                .Where(b => b["type"]?.GetValue<string>() == "text")
                .Select(b => b["text"]?.GetValue<string>() ?? ""));
        return content?.ToString() ?? "";
    }

    private IReadOnlyList<AskEvent> ReadFinalResult(JsonObject json)
    {
        var isError = json["is_error"]?.GetValue<bool>() ?? false;
        var subtype = json["subtype"]?.GetValue<string>() ?? "";
        var text = json["result"]?.GetValue<string>() ?? "";
        var turns = json["num_turns"]?.GetValue<int>() ?? 0;
        var usage = json["usage"] as JsonObject;
        var input = (usage?["input_tokens"]?.GetValue<long>() ?? 0)
            + (usage?["cache_read_input_tokens"]?.GetValue<long>() ?? 0)
            + (usage?["cache_creation_input_tokens"]?.GetValue<long>() ?? 0);
        var output = usage?["output_tokens"]?.GetValue<long>() ?? 0;
        var sessionId = json["session_id"]?.GetValue<string>();
        Result = new ClaudeResult(isError, subtype, text, turns, input, output, sessionId);
        return [];
    }
}
