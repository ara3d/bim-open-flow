using System.Text.Json;
using System.Text.Json.Nodes;
using Ara3D.MCP;

namespace BimOpenFlow.Studio;

/// <summary>What the agent reports as it works: a tool call it made (with the
/// arguments, whether it succeeded, and a one-line summary of the result) or
/// text it wrote between calls.</summary>
public sealed record AskEvent(string Type, string? Name = null, JsonNode? Args = null, bool? Ok = null,
    string? Summary = null, string? Text = null);

public sealed record AskOutcome(string Text, int Turns, long InputTokens, long OutputTokens);

/// <summary>The agent loop: the model sees the MCP server's tools as functions,
/// every call it makes goes through the server's own JSON-RPC handler in
/// process, and the result text goes back as the tool message. Ends when the
/// model answers without calling a tool.</summary>
public sealed class AskAgent(McpServer tools, OpenAiChat chat, int maxTurns = AskAgent.DefaultMaxTurns)
{
    public const int DefaultMaxTurns = 60;
    private const int SummaryLength = 160;
    private const string RepeatNote =
        " Note from the host: this is the same call as before and it returned the same result; nothing has changed. "
        + "Repeating it will not help. Make a different edit, read a different node's result, or explain the outcome.";
    private int _nextId;
    private (string Call, string Result)? _last;

    /// <summary>Starts a conversation: system prompt plus the first request.</summary>
    public static JsonArray NewConversation(string system)
        => [new JsonObject { ["role"] = "system", ["content"] = system }];

    public Task<AskOutcome> RunAsync(string system, string user, Func<AskEvent, Task> emit, CancellationToken ct)
        => RunAsync(NewConversation(system), user, emit, ct);

    /// <summary>Appends the user message to an existing conversation (so a
    /// follow-up sees everything the agent did before) and runs until the model
    /// answers. The conversation is mutated in place and can be continued again.</summary>
    public async Task<AskOutcome> RunAsync(JsonArray messages, string user, Func<AskEvent, Task> emit, CancellationToken ct)
    {
        var functions = ListFunctions();
        messages.Add(new JsonObject { ["role"] = "user", ["content"] = user });
        long input = 0, output = 0;
        for (var turn = 1; turn <= maxTurns; turn++)
        {
            ct.ThrowIfCancellationRequested();
            var response = await chat.CompleteAsync(messages, functions, ct);
            input += response["usage"]?["prompt_tokens"]?.GetValue<long>() ?? 0;
            output += response["usage"]?["completion_tokens"]?.GetValue<long>() ?? 0;
            var message = response["choices"]?[0]?["message"] as JsonObject
                ?? throw new InvalidOperationException("OpenAI returned no message.");
            messages.Add(message.DeepClone());

            var text = message["content"]?.GetValue<string>();
            var calls = message["tool_calls"] as JsonArray;
            if (calls is null || calls.Count == 0)
                return new AskOutcome(text?.Trim() ?? "", turn, input, output);
            if (!string.IsNullOrWhiteSpace(text))
                await emit(new AskEvent("text", Text: text.Trim()));

            foreach (var call in calls.OfType<JsonObject>())
            {
                var callId = call["id"]?.GetValue<string>() ?? "";
                var name = call["function"]?["name"]?.GetValue<string>() ?? "";
                var args = ParseArguments(call["function"]?["arguments"]?.GetValue<string>());
                var (ok, resultText, summary) = Execute(name, args);
                var signature = (Call: name + args.ToJsonString(), Result: resultText);
                var repeated = _last == signature;
                _last = signature;
                await emit(new AskEvent("tool", name, args, ok, repeated ? summary + " (repeated, unchanged)" : summary));
                messages.Add(new JsonObject
                {
                    ["role"] = "tool",
                    ["tool_call_id"] = callId,
                    ["content"] = repeated ? resultText + RepeatNote : resultText,
                });
            }
        }
        throw new InvalidOperationException($"Stopped after {maxTurns} model turns without a final answer.");
    }

    /// <summary>The server's tools/list, reshaped as OpenAI function tools.</summary>
    public JsonArray ListFunctions()
    {
        var listed = Rpc("tools/list", new JsonObject());
        var result = new JsonArray();
        foreach (var tool in listed["result"]?["tools"]?.AsArray().OfType<JsonObject>() ?? [])
        {
            result.Add(new JsonObject
            {
                ["type"] = "function",
                ["function"] = new JsonObject
                {
                    ["name"] = tool["name"]?.DeepClone(),
                    ["description"] = tool["description"]?.DeepClone(),
                    ["parameters"] = tool["inputSchema"]?.DeepClone() ?? new JsonObject { ["type"] = "object" },
                },
            });
        }
        return result;
    }

    /// <summary>Runs one tool call; never throws, because the model needs the
    /// failure text to recover.</summary>
    public (bool Ok, string Result, string Summary) Execute(string name, JsonObject args)
    {
        JsonObject response;
        try
        {
            response = Rpc("tools/call", new JsonObject { ["name"] = name, ["arguments"] = args.DeepClone() });
        }
        catch (Exception e)
        {
            return (false, $"{{\"ok\":false,\"error\":{JsonSerializer.Serialize(e.Message)}}}", e.Message);
        }
        if (response["error"] is JsonObject rpcError)
        {
            var message = rpcError["message"]?.GetValue<string>() ?? "unknown error";
            return (false, $"{{\"ok\":false,\"error\":{JsonSerializer.Serialize(message)}}}", message);
        }
        var text = response["result"]?["content"]?[0]?["text"]?.GetValue<string>() ?? "";
        var envelope = TryParse(text);
        if (envelope is null)
            return (false, text, Truncate(text, SummaryLength));
        var ok = envelope["ok"]?.GetValue<bool>() ?? false;
        // Compact: the server pretty-prints, and the model pays per token.
        return (ok, envelope.ToJsonString(), ok ? Summarize(name, envelope["data"]) : envelope["error"]?.GetValue<string>() ?? text);
    }

    private JsonObject Rpc(string method, JsonObject parameters)
    {
        var request = new JsonObject
        {
            ["jsonrpc"] = "2.0",
            ["id"] = Interlocked.Increment(ref _nextId),
            ["method"] = method,
            ["params"] = parameters,
        };
        var result = tools.HandlePost(request.ToJsonString());
        return JsonNode.Parse(result.JsonBody) as JsonObject
            ?? throw new InvalidOperationException($"MCP {method} returned no JSON object (status {result.StatusCode}).");
    }

    private static JsonObject ParseArguments(string? arguments)
    {
        if (string.IsNullOrWhiteSpace(arguments))
            return new JsonObject();
        try
        {
            return JsonNode.Parse(arguments) as JsonObject ?? new JsonObject();
        }
        catch (JsonException)
        {
            return new JsonObject();
        }
    }

    private static JsonObject? TryParse(string text)
    {
        try
        {
            return JsonNode.Parse(text) as JsonObject;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>One line per tool for the transcript: node states for evaluate,
    /// row counts for getResult, table counts for describeDatabase, and the
    /// start of the data for everything else.</summary>
    public static string Summarize(string name, JsonNode? data)
    {
        if (data is not JsonObject obj)
            return Truncate(data?.ToJsonString() ?? "", SummaryLength);
        if (obj["graphHash"] is not null && obj.Count <= 2)
            return "saved";
        if (name == "evaluate" && obj["nodes"] is JsonArray nodes)
        {
            var states = nodes.OfType<JsonObject>()
                .Select(n => (Id: n["nodeId"]?.GetValue<string>() ?? "?", Status: n["status"]?.GetValue<string>() ?? "?",
                    Error: n["error"]?.GetValue<string>()))
                .ToList();
            var bad = states.Where(s => s.Status != "Ok").ToList();
            return bad.Count == 0
                ? $"{states.Count} nodes Ok"
                : $"{states.Count - bad.Count} Ok; " + string.Join("; ", bad.Select(s =>
                    $"{s.Id} {s.Status}{(string.IsNullOrEmpty(s.Error) ? "" : ": " + Truncate(s.Error, 100))}"));
        }
        if (name == "getResult" && obj["columns"] is JsonArray columns)
        {
            var names = columns.OfType<JsonObject>().Select(c => c["name"]?.GetValue<string>()).ToList();
            return $"{obj["totalRows"]?.GetValue<long>() ?? 0} rows; columns {string.Join(", ", names)}";
        }
        if (name == "describeDatabase" && obj["tables"] is JsonArray tables)
        {
            var first = tables.Count == 1 ? tables[0] as JsonObject : null;
            var typed = first?["columns"] is JsonArray cols && cols.Count > 0 && cols[0] is JsonObject;
            return typed
                ? $"{first!["name"]} has {first["columns"]!.AsArray().Count} columns"
                : $"{tables.Count} tables";
        }
        return Truncate(obj.ToJsonString(), SummaryLength);
    }

    private static string Truncate(string text, int length)
        => text.Length <= length ? text : text[..length] + "…";
}
