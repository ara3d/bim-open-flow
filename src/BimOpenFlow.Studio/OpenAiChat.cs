using System.Net.Http.Headers;
using System.Text;
using System.Text.Json.Nodes;

namespace BimOpenFlow.Studio;

/// <summary>One chat-completions call to the OpenAI API with function tools,
/// over a plain HttpClient. Requests and responses stay as JSON nodes: the agent
/// loop appends the assistant message it gets back verbatim, so nothing here
/// needs to know every field the API might add.</summary>
public sealed class OpenAiChat(HttpClient http, string apiKey, string model, string? endpoint = null)
{
    public const string DefaultModel = "gpt-5";
    public const string DefaultEndpoint = "https://api.openai.com/v1/chat/completions";
    public const string KeyVariable = "OPENAI_API_KEY";
    public const string KeyFileVariable = "OPENAI_API_KEY_FILE";
    public const string ModelVariable = "OPENAI_MODEL";

    public string Model { get; } = model;

    private readonly string _endpoint = endpoint ?? DefaultEndpoint;

    /// <summary>The key from OPENAI_API_KEY, else the first line of the file named by
    /// OPENAI_API_KEY_FILE; null when neither is set. The file form keeps the key
    /// out of shell histories and process listings.</summary>
    public static string? ResolveApiKey(Func<string, string?>? environment = null)
    {
        var env = environment ?? Environment.GetEnvironmentVariable;
        var key = env(KeyVariable);
        if (!string.IsNullOrWhiteSpace(key))
            return key.Trim();
        var file = env(KeyFileVariable);
        if (string.IsNullOrWhiteSpace(file))
            return null;
        if (!File.Exists(file))
            throw new FileNotFoundException($"{KeyFileVariable} points to a missing file: {file}", file);
        var line = File.ReadLines(file).Select(l => l.Trim()).FirstOrDefault(l => l.Length > 0);
        return string.IsNullOrEmpty(line) ? null : line;
    }

    public static string ResolveModel(Func<string, string?>? environment = null)
    {
        var value = (environment ?? Environment.GetEnvironmentVariable)(ModelVariable);
        return string.IsNullOrWhiteSpace(value) ? DefaultModel : value.Trim();
    }

    /// <summary>Sends the conversation and tool list; returns the whole response
    /// object (choices, usage). Non-success statuses become exceptions carrying the
    /// API's own error message.</summary>
    public async Task<JsonObject> CompleteAsync(JsonArray messages, JsonArray tools, CancellationToken ct)
    {
        var body = new JsonObject
        {
            ["model"] = Model,
            ["messages"] = messages.DeepClone(),
            ["tools"] = tools.DeepClone(),
            ["tool_choice"] = "auto",
        };
        using var request = new HttpRequestMessage(HttpMethod.Post, _endpoint);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        request.Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json");
        using var response = await http.SendAsync(request, ct);
        var text = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException($"OpenAI {(int)response.StatusCode}: {ErrorMessage(text)}");
        return JsonNode.Parse(text) as JsonObject
            ?? throw new InvalidOperationException("OpenAI returned a non-object response.");
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
