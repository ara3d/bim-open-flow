namespace BimOpenFlow.Ask;

/// <summary>Which provider the Ask agent talks to, decided from the environment once:
/// ASK_PROVIDER names one ('anthropic' or 'openai'); otherwise the first provider with a key,
/// Anthropic before OpenAI. <see cref="Problem"/> is the sentence to show when no usable key
/// exists, so a host can report it on startup and on every request without a model call.</summary>
public sealed record ChatSelection(string Provider, string Model, string? Problem)
{
    public const string ProviderVariable = "ASK_PROVIDER";

    public const string NoKey =
        "No model key: set ANTHROPIC_API_KEY (or ANTHROPIC_API_KEY_FILE to a file whose first line is the key) "
        + "for Claude, or OPENAI_API_KEY / OPENAI_API_KEY_FILE for OpenAI, then restart.";

    public bool Configured => Problem is null;

    public static ChatSelection Resolve(Func<string, string?>? environment = null)
    {
        var env = environment ?? Environment.GetEnvironmentVariable;
        var requested = env(ProviderVariable)?.Trim().ToLowerInvariant();
        try
        {
            var anthropic = requested is null or AnthropicChat.ProviderName && AnthropicChat.ResolveApiKey(env) is not null;
            if (anthropic)
                return new(AnthropicChat.ProviderName, AnthropicChat.ResolveModel(env), null);
            var openai = requested is null or OpenAiChat.ProviderName && OpenAiChat.ResolveApiKey(env) is not null;
            if (openai)
                return new(OpenAiChat.ProviderName, OpenAiChat.ResolveModel(env), null);
            return requested is null or AnthropicChat.ProviderName or OpenAiChat.ProviderName
                ? new(requested ?? AnthropicChat.ProviderName, "", NoKey)
                : new(requested, "", $"{ProviderVariable}='{requested}' is not a provider; use anthropic or openai.");
        }
        catch (Exception e)
        {
            return new(requested ?? AnthropicChat.ProviderName, "", e.Message);
        }
    }

    /// <summary>The client for this selection; throws the <see cref="Problem"/> when there is one.</summary>
    public IChatModel Create(HttpClient http, Func<string, string?>? environment = null)
    {
        if (Problem is not null)
            throw new InvalidOperationException(Problem);
        return Provider == AnthropicChat.ProviderName
            ? new AnthropicChat(http, AnthropicChat.ResolveApiKey(environment)!, Model)
            : new OpenAiChat(http, OpenAiChat.ResolveApiKey(environment)!, Model);
    }
}
