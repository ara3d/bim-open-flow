using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace BimOpenFlow.Ask;

/// <summary>One Messages API call to Claude over a plain HttpClient, translated from and back
/// to the agent loop's conversation shape (see <see cref="IChatModel"/>). The 'system' message
/// becomes the request's system block with a cache breakpoint, since the prompt (catalog and
/// guides) is long and identical across requests; 'tool' messages become tool_result blocks;
/// and the raw content Claude returned is kept on the assistant message as 'content_blocks'
/// so thinking blocks go back unchanged on the next turn.</summary>
public sealed class AnthropicChat(HttpClient http, string apiKey, string model, string? endpoint = null) : IChatModel
{
    public const string ProviderName = "anthropic";
    public const string DefaultModel = "claude-opus-5";
    public const string DefaultEndpoint = "https://api.anthropic.com/v1/messages";
    public const string ApiVersion = "2023-06-01";
    public const string KeyVariable = "ANTHROPIC_API_KEY";
    public const string KeyFileVariable = "ANTHROPIC_API_KEY_FILE";
    public const string ModelVariable = "ANTHROPIC_MODEL";
    public const string EffortVariable = "ANTHROPIC_EFFORT";
    public const string BaseUrlVariable = "ANTHROPIC_BASE_URL";
    public const int MaxTokens = 16000;
    private const string ContentBlocksKey = "content_blocks";

    public string Provider => ProviderName;
    public string Model { get; } = model;

    /// <summary>Effort for the reply (low, medium, high, xhigh, max); null takes the model's
    /// default. From ANTHROPIC_EFFORT.</summary>
    public string? Effort { get; init; } = ResolveEffort();

    private readonly string _endpoint = endpoint ?? ResolveEndpoint();

    public static string? ResolveApiKey(Func<string, string?>? environment = null)
        => ApiKeys.Resolve(KeyVariable, KeyFileVariable, environment);

    public static string ResolveModel(Func<string, string?>? environment = null)
        => ApiKeys.ValueOr(ModelVariable, DefaultModel, environment);

    public static string? ResolveEffort(Func<string, string?>? environment = null)
    {
        var value = (environment ?? Environment.GetEnvironmentVariable)(EffortVariable);
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToLowerInvariant();
    }

    /// <summary>ANTHROPIC_BASE_URL, as the official SDKs honour it, else the public API.</summary>
    public static string ResolveEndpoint(Func<string, string?>? environment = null)
    {
        var baseUrl = (environment ?? Environment.GetEnvironmentVariable)(BaseUrlVariable);
        return string.IsNullOrWhiteSpace(baseUrl) ? DefaultEndpoint : baseUrl.Trim().TrimEnd('/') + "/v1/messages";
    }

    public async Task<JsonObject> CompleteAsync(JsonArray messages, JsonArray tools, CancellationToken ct)
    {
        var body = BuildRequest(messages, tools);
        using var request = new HttpRequestMessage(HttpMethod.Post, _endpoint);
        request.Headers.Add("x-api-key", apiKey);
        request.Headers.Add("anthropic-version", ApiVersion);
        request.Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json");
        using var response = await http.SendAsync(request, ct);
        var text = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException($"Anthropic {(int)response.StatusCode}: {ErrorMessage(text)}");
        var reply = JsonNode.Parse(text) as JsonObject
            ?? throw new InvalidOperationException("Anthropic returned a non-object response.");
        return ToCompletion(reply);
    }

    /// <summary>The Messages API request for a conversation in the loop's shape.</summary>
    public JsonObject BuildRequest(JsonArray messages, JsonArray tools)
    {
        var body = new JsonObject
        {
            ["model"] = Model,
            ["max_tokens"] = MaxTokens,
            ["messages"] = ToMessages(messages),
            ["tools"] = ToTools(tools),
        };
        var system = messages.OfType<JsonObject>()
            .Where(m => m["role"]?.GetValue<string>() == "system")
            .Select(m => m["content"]?.GetValue<string>())
            .FirstOrDefault(s => !string.IsNullOrEmpty(s));
        if (system is not null)
            body["system"] = new JsonArray(new JsonObject
            {
                ["type"] = "text",
                ["text"] = system,
                ["cache_control"] = new JsonObject { ["type"] = "ephemeral" },
            });
        if (Effort is not null)
            body["output_config"] = new JsonObject { ["effort"] = Effort };
        return body;
    }

    /// <summary>A Messages API reply as a chat-completions response: text and tool_use blocks
    /// become content and tool_calls, the raw blocks ride along for replay, and usage counts
    /// cached input as prompt tokens so the totals mean what they do for OpenAI.</summary>
    public static JsonObject ToCompletion(JsonObject reply)
    {
        var blocks = reply["content"] as JsonArray ?? [];
        var text = string.Join("\n", blocks.OfType<JsonObject>()
            .Where(b => b["type"]?.GetValue<string>() == "text")
            .Select(b => b["text"]?.GetValue<string>())
            .Where(t => !string.IsNullOrEmpty(t)));
        var calls = new JsonArray();
        foreach (var block in blocks.OfType<JsonObject>().Where(b => b["type"]?.GetValue<string>() == "tool_use"))
            calls.Add(new JsonObject
            {
                ["id"] = block["id"]?.DeepClone(),
                ["type"] = "function",
                ["function"] = new JsonObject
                {
                    ["name"] = block["name"]?.DeepClone(),
                    ["arguments"] = (block["input"] ?? new JsonObject()).ToJsonString(),
                },
            });
        if (reply["stop_reason"]?.GetValue<string>() == "refusal")
        {
            var category = reply["stop_details"]?["category"]?.GetValue<string>();
            text = $"The model declined this request{(category is null ? "" : $" ({category})")}. {text}".Trim();
            calls.Clear();
        }
        var message = new JsonObject
        {
            ["role"] = "assistant",
            ["content"] = text.Length == 0 ? null : text,
            [ContentBlocksKey] = blocks.DeepClone(),
        };
        if (calls.Count > 0)
            message["tool_calls"] = calls;
        var usage = reply["usage"] as JsonObject;
        long Count(string name) => usage?[name]?.GetValue<long>() ?? 0;
        return new JsonObject
        {
            ["choices"] = new JsonArray(new JsonObject
            {
                ["finish_reason"] = calls.Count > 0 ? "tool_calls" : "stop",
                ["message"] = message,
            }),
            ["usage"] = new JsonObject
            {
                ["prompt_tokens"] = Count("input_tokens") + Count("cache_read_input_tokens") + Count("cache_creation_input_tokens"),
                ["completion_tokens"] = Count("output_tokens"),
            },
        };
    }

    /// <summary>Loop messages to Messages API messages. Consecutive 'tool' messages (parallel
    /// calls) merge into one user message, as the API requires.</summary>
    public static JsonArray ToMessages(JsonArray messages)
    {
        var result = new JsonArray();
        JsonArray? pendingResults = null;
        foreach (var message in messages.OfType<JsonObject>())
        {
            var role = message["role"]?.GetValue<string>();
            if (role == "tool")
            {
                if (pendingResults is null)
                {
                    pendingResults = [];
                    result.Add(new JsonObject { ["role"] = "user", ["content"] = pendingResults });
                }
                pendingResults.Add(new JsonObject
                {
                    ["type"] = "tool_result",
                    ["tool_use_id"] = message["tool_call_id"]?.DeepClone(),
                    ["content"] = message["content"]?.DeepClone() ?? "",
                });
                continue;
            }
            pendingResults = null;
            switch (role)
            {
                case "user":
                    result.Add(new JsonObject { ["role"] = "user", ["content"] = message["content"]?.DeepClone() ?? "" });
                    break;
                case "assistant":
                    result.Add(new JsonObject { ["role"] = "assistant", ["content"] = AssistantContent(message) });
                    break;
            }
        }
        return result;
    }

    /// <summary>The blocks Claude produced, when this provider produced the message; otherwise
    /// rebuilt from the loop shape (a conversation started elsewhere, or a test).</summary>
    private static JsonArray AssistantContent(JsonObject message)
    {
        if (message[ContentBlocksKey] is JsonArray blocks && blocks.Count > 0)
            return (JsonArray)blocks.DeepClone();
        var content = new JsonArray();
        var text = message["content"]?.GetValue<string>();
        if (!string.IsNullOrEmpty(text))
            content.Add(new JsonObject { ["type"] = "text", ["text"] = text });
        foreach (var call in (message["tool_calls"] as JsonArray ?? []).OfType<JsonObject>())
            content.Add(new JsonObject
            {
                ["type"] = "tool_use",
                ["id"] = call["id"]?.DeepClone(),
                ["name"] = call["function"]?["name"]?.DeepClone(),
                ["input"] = ParseArguments(call["function"]?["arguments"]?.GetValue<string>()),
            });
        return content;
    }

    public static JsonArray ToTools(JsonArray tools)
    {
        var result = new JsonArray();
        foreach (var tool in tools.OfType<JsonObject>())
        {
            var function = tool["function"] as JsonObject ?? tool;
            result.Add(new JsonObject
            {
                ["name"] = function["name"]?.DeepClone(),
                ["description"] = function["description"]?.DeepClone() ?? "",
                ["input_schema"] = function["parameters"]?.DeepClone() ?? new JsonObject { ["type"] = "object" },
            });
        }
        return result;
    }

    private static JsonNode ParseArguments(string? arguments)
    {
        if (string.IsNullOrWhiteSpace(arguments))
            return new JsonObject();
        try
        {
            return JsonNode.Parse(arguments) ?? new JsonObject();
        }
        catch (JsonException)
        {
            return new JsonObject();
        }
    }

    private static string ErrorMessage(string body)
    {
        try
        {
            var message = JsonNode.Parse(body)?["error"]?["message"]?.GetValue<string>();
            if (!string.IsNullOrWhiteSpace(message))
                return message;
        }
        catch (Exception)
        {
            // Not JSON; fall through to the raw body.
        }
        return body.Length > 300 ? body[..300] : body;
    }
}
