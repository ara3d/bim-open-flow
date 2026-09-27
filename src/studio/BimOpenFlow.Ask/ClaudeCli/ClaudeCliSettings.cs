namespace BimOpenFlow.Ask;

public sealed record ClaudeCliSettings(string Executable, string Model, string Effort, string WorkDirectory)
{
    public const string ModelVariable = "ASK_CLAUDE_MODEL";
    public const string EffortVariable = "ASK_CLAUDE_EFFORT";
    public const string DefaultModel = "claude-haiku-4-5-20251001";
    public const string DefaultEffort = "medium";
    public static string DefaultWorkDirectory => Path.Combine(Path.GetTempPath(), "bimopenflow-ask", "claude-cli");
    public TimeSpan Timeout { get; init; } = TimeSpan.FromMinutes(10);
}
