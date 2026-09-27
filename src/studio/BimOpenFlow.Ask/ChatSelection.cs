namespace BimOpenFlow.Ask;

/// <summary>Which backend the Ask agent talks to, decided from the environment once: ASK_PROVIDER
/// names one ('claude-cli', 'anthropic', or 'openai'); otherwise claude-cli when its executable is
/// found (the owner's retired Anthropic API account must never be reached by a leftover key);
/// otherwise the first API provider with a key, Anthropic before OpenAI; otherwise
/// <see cref="NoProvider"/>. <see cref="Problem"/> is the sentence to show when no backend is
/// usable, so a host can report it on startup and on every request without a model call.</summary>
public sealed record ChatSelection(string Provider, string Model, string? Problem)
{
    public const string ProviderVariable = "ASK_PROVIDER";

    /// <summary>Effort for the reply: for claude-cli, ASK_CLAUDE_EFFORT or
    /// ClaudeCliSettings.DefaultEffort; for anthropic, ANTHROPIC_EFFORT; for openai,
    /// OPENAI_REASONING_EFFORT. Null when the API provider has none set.</summary>
    public string? Effort { get; init; }

    /// <summary>The claude executable this selection resolved to; null for the API providers.</summary>
    public string? Executable { get; init; }

    public const string NoProvider =
        "No model available: install Claude Code (npm install -g @anthropic-ai/claude-code) and run "
        + "'claude' then '/login', or set ASK_CLAUDE_CLI to its path, or set ANTHROPIC_API_KEY "
        + "(or OPENAI_API_KEY) and restart.";

    public const string ClaudeCliMissing =
        "ASK_PROVIDER=claude-cli but no claude executable was found: install Claude Code "
        + "(npm install -g @anthropic-ai/claude-code) and run 'claude' then '/login', or set "
        + "ASK_CLAUDE_CLI to its path.";

    public bool Configured => Problem is null;

    public static ChatSelection Resolve(Func<string, string?>? environment = null)
    {
        var env = environment ?? Environment.GetEnvironmentVariable;
        var requested = env(ProviderVariable)?.Trim().ToLowerInvariant();
        try
        {
            if (requested is not null
                && requested != ClaudeCliBackend.ProviderName
                && requested != AnthropicChat.ProviderName
                && requested != OpenAiChat.ProviderName)
                return new(requested, "",
                    $"{ProviderVariable}='{requested}' is not a provider; use anthropic, openai, or claude-cli.");

            if (requested is null or ClaudeCliBackend.ProviderName)
            {
                var executable = ClaudeCliLocator.Find(env);
                if (executable is not null)
                {
                    var model = ApiKeys.ValueOr(ClaudeCliSettings.ModelVariable, ClaudeCliSettings.DefaultModel, env);
                    var effort = ApiKeys.ValueOr(ClaudeCliSettings.EffortVariable, ClaudeCliSettings.DefaultEffort, env);
                    return new(ClaudeCliBackend.ProviderName, model, null) { Effort = effort, Executable = executable };
                }
                if (requested == ClaudeCliBackend.ProviderName)
                    return new(ClaudeCliBackend.ProviderName, "", ClaudeCliMissing);
            }

            if (requested is null or AnthropicChat.ProviderName && AnthropicChat.ResolveApiKey(env) is not null)
                return new(AnthropicChat.ProviderName, AnthropicChat.ResolveModel(env), null)
                {
                    Effort = AnthropicChat.ResolveEffort(env),
                };

            if (requested is null or OpenAiChat.ProviderName && OpenAiChat.ResolveApiKey(env) is not null)
                return new(OpenAiChat.ProviderName, OpenAiChat.ResolveModel(env), null)
                {
                    Effort = OpenAiChat.ResolveReasoningEffort(env),
                };

            return new(requested ?? ClaudeCliBackend.ProviderName, "", NoProvider);
        }
        catch (Exception e)
        {
            return new(requested ?? ClaudeCliBackend.ProviderName, "", e.Message);
        }
    }

    /// <summary>The API client for this selection; throws the <see cref="Problem"/> when there is
    /// one, and throws for claude-cli, which has no <see cref="IChatModel"/> (use
    /// <see cref="CreateBackend"/>).</summary>
    public IChatModel Create(HttpClient http, Func<string, string?>? environment = null)
    {
        if (Problem is not null)
            throw new InvalidOperationException(Problem);
        if (Provider == ClaudeCliBackend.ProviderName)
            throw new InvalidOperationException($"{ClaudeCliBackend.ProviderName} has no {nameof(IChatModel)}; use {nameof(CreateBackend)}.");
        return Provider == AnthropicChat.ProviderName
            ? new AnthropicChat(http, AnthropicChat.ResolveApiKey(environment)!, Model) { Effort = Effort }
            : new OpenAiChat(http, OpenAiChat.ResolveApiKey(environment)!, Model) { ReasoningEffort = Effort };
    }

    /// <summary>The backend for this selection: a <see cref="ClaudeCliBackend"/> against
    /// <see cref="Executable"/> for claude-cli, else a <see cref="ChatBackend"/> over
    /// <see cref="Create"/>. Throws the <see cref="Problem"/> when there is one.</summary>
    public IAskBackend CreateBackend(AskSetup setup, HttpClient http, Func<string, string?>? environment = null)
    {
        if (Problem is not null)
            throw new InvalidOperationException(Problem);
        if (Provider == ClaudeCliBackend.ProviderName)
            return new ClaudeCliBackend(setup,
                new ClaudeCliSettings(Executable!, Model, Effort ?? ClaudeCliSettings.DefaultEffort, ClaudeCliSettings.DefaultWorkDirectory));
        return new ChatBackend(setup, Create(http, environment));
    }
}
